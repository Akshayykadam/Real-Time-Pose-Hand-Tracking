using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Controller for full body skeleton visualization using MediaPipe pose landmarks.
    /// Features: Parallel Job System filtering, adaptive FPS-based skipping, occlusion prediction.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FullBodySkeletonController : SimplePoseAnnotationController
    {
        [Header("Skeleton Settings")]
        [SerializeField] private float _visibilityThreshold = 0.3f;
        [SerializeField] private bool _showSkeleton = true;

        [Header("Performance")]
        [Tooltip("Target FPS for processing. 0 = Use screen limit.")]
        [SerializeField] private int _targetFPS = 60;
        
        [Tooltip("Automatically skip frames if FPS drops below 30")]
        [SerializeField] private bool _adaptiveFrameSkipping = true;
        
        [Tooltip("Use Unity Job System for filtering (Recommended for multiple people)")]
        [SerializeField] private bool _useJobSystem = true;
        
        [Header("Filtering Mode")]
        [Tooltip("Type of filter to use for jitter reduction")]
        [SerializeField] private LandmarkFilterManager.FilterType _filterType = LandmarkFilterManager.FilterType.OneEuro;
        
        [Tooltip("Preset for One-Euro filter")]
        [SerializeField] private LandmarkFilterManager.FilterPreset _filterPreset = LandmarkFilterManager.FilterPreset.Balanced;
        
        [Header("Occlusion & Prediction")]
        [SerializeField] private bool _enableOcclusionPersistence = true;
        [SerializeField, Range(1, 60)] private int _occlusionPersistenceFrames = 15;
        
        [Header("Adaptive Smoothing")]
        [SerializeField] private bool _enableAdaptiveSmoothing = true;
        [SerializeField, Range(0.1f, 2f)] private float _fastMovementThreshold = 0.5f;

        private RectTransform _rectTransform;
        private int _frameSkip = 0;
        private int _frameCounter = 0;
        private bool _lastPoseDetected = false;

        // Managers
        private LandmarkFilterManager _filterManager;
        
        // Performance Monitoring
        private float _fpsAccumulator = 0f;
        private int _fpsFrames = 0;
        private float _currentFPS = 60f;
        
        // Job System Buffers (NativeArrays) for parallel processing
        // We only support parallel filtering for primary pose (index 0) to simplify improved buffer management
        private const int MAX_JOB_LANDMARKS = 33;
        private NativeArray<float3> _jobRawPositions;
        private NativeArray<float> _jobTimestamps;
        private NativeArray<float3> _jobFilteredPositions;
        private NativeArray<bool> _jobInitialized;
        
        // Internal State
        private NativeArray<float3> _jobLastRawPositions;
        private NativeArray<float> _jobInternalState; // OneEuro state
        private NativeArray<float> _jobKalmanState;   // Kalman state
        private NativeArray<float> _jobCovariance;    // Kalman Covariance
        
        private bool _jobsInitialized = false;

        // Events
        public event System.Action<int, int, LandmarkFilterManager.LandmarkState> OnLandmarkUpdated;
        public event System.Action<bool> OnPoseDetectionChanged;

        // Legacy/Cache containers
        private Dictionary<int, Vector3[]> _cachedPositions = new Dictionary<int, Vector3[]>();
        private Dictionary<int, float[]> _cachedVisibility = new Dictionary<int, float[]>();

        private static readonly Vector2 _stretchPivot = new Vector2(0.5f, 0.5f);
        private static readonly Vector3 _zeroPosition = Vector3.zero;

        protected override void Start()
        {
            base.Start();

            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }

            _rectTransform.pivot = _stretchPivot;
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.anchoredPosition3D = _zeroPosition;

            // Initialize Filter Manager (for non-job fallback and management logic)
            _filterManager = new LandmarkFilterManager(_filterPreset);
            _filterManager.SetFilterType(_filterType);
            _filterManager.SetOcclusionHandling(_enableOcclusionPersistence, _occlusionPersistenceFrames, _visibilityThreshold);
            
            if (_useJobSystem) InitializeJobBuffers();

            if (annotation != null)
            {
                annotation.gameObject.SetActive(_showSkeleton);
            }
        }
        
        private void InitializeJobBuffers()
        {
            if (_jobsInitialized) DisposeJobBuffers();
            
            _jobRawPositions = new NativeArray<float3>(MAX_JOB_LANDMARKS, Allocator.Persistent);
            _jobTimestamps = new NativeArray<float>(MAX_JOB_LANDMARKS, Allocator.Persistent);
            _jobFilteredPositions = new NativeArray<float3>(MAX_JOB_LANDMARKS, Allocator.Persistent);
            _jobInitialized = new NativeArray<bool>(MAX_JOB_LANDMARKS, Allocator.Persistent);
            _jobLastRawPositions = new NativeArray<float3>(MAX_JOB_LANDMARKS, Allocator.Persistent);
            
            // OneEuro needs 6 floats per landmark [val, deriv] * 3 dims
            _jobInternalState = new NativeArray<float>(MAX_JOB_LANDMARKS * 6, Allocator.Persistent);
            
            // Kalman needs 9 state + 9 covariance per landmark
            _jobKalmanState = new NativeArray<float>(MAX_JOB_LANDMARKS * 9, Allocator.Persistent);
            _jobCovariance = new NativeArray<float>(MAX_JOB_LANDMARKS * 9, Allocator.Persistent);
            
            _jobsInitialized = true;
        }

        private void DisposeJobBuffers()
        {
            if (!_jobsInitialized) return;
            
            if (_jobRawPositions.IsCreated) _jobRawPositions.Dispose();
            if (_jobTimestamps.IsCreated) _jobTimestamps.Dispose();
            if (_jobFilteredPositions.IsCreated) _jobFilteredPositions.Dispose();
            if (_jobInitialized.IsCreated) _jobInitialized.Dispose();
            if (_jobLastRawPositions.IsCreated) _jobLastRawPositions.Dispose();
            if (_jobInternalState.IsCreated) _jobInternalState.Dispose();
            if (_jobKalmanState.IsCreated) _jobKalmanState.Dispose();
            if (_jobCovariance.IsCreated) _jobCovariance.Dispose();
            
            _jobsInitialized = false;
        }

        private void OnDestroy()
        {
            DisposeJobBuffers();
        }

        protected override void SyncNow()
        {
            // --- Adaptive Frame Skipping ---
            UpdatePerformanceStats();
            if (_adaptiveFrameSkipping)
            {
                if (_currentFPS < 30f) _frameSkip = 2;       // processing heavy, skip 2 frames (process 20fps)
                else if (_currentFPS < 45f) _frameSkip = 1;  // struggling, skip 1 frame (process 30fps)
                else _frameSkip = 0;                         // stable, process all
            }
            
            _frameCounter++;
            if (_frameSkip > 0 && (_frameCounter % (_frameSkip + 1)) != 0) return;


            lock (_currentTargetLock)
            {
                isStale = false;
                bool hasPose = _currentTarget.poseLandmarks != null && _currentTarget.poseLandmarks.Count > 0;

                if (!_showSkeleton)
                {
                    if (annotation != null && annotation.gameObject.activeSelf) 
                        annotation.gameObject.SetActive(false);
                    return;
                }

                if (hasPose != _lastPoseDetected)
                {
                    _lastPoseDetected = hasPose;
                    if (annotation != null) annotation.gameObject.SetActive(hasPose);
                    OnPoseDetectionChanged?.Invoke(hasPose);
                    
                    if (!hasPose)
                    {
                        _filterManager.Reset();
                        // Reset jobs if needed
                         if (_useJobSystem && _jobsInitialized)
                         {
                             // Mark all as uninitialized to trigger reset on next valid frame
                             for(int i=0; i<MAX_JOB_LANDMARKS; i++) _jobInitialized[i] = false;
                         }
                    }
                }

                if (hasPose && annotation != null)
                {
                    if (_useJobSystem && _jobsInitialized && _currentTarget.poseLandmarks.Count > 0)
                    {
                        // Job System path (Primary pose only for now)
                        ProcessFilteringJobs(_currentTarget.poseLandmarks[0].landmarks);
                        
                        // Fallback for secondary poses using standard manager
                        if (_currentTarget.poseLandmarks.Count > 1)
                        {
                            // Skip index 0, process others
                        }
                    }
                    else
                    {
                        // Standard path
                        UpdateFiltering(_currentTarget.poseLandmarks);
                    }
                    
                    annotation.Draw(_currentTarget.poseLandmarks, false);
                }
            }
        }
        
        // Use explicit type to avoid namespace collision or wrong type inference
        private void ProcessFilteringJobs(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks)
        {
            float timestamp = Time.time;
            int count = Mathf.Min(landmarks.Count, MAX_JOB_LANDMARKS);

            // 1. Prepare Input Data
            for (int i = 0; i < count; i++)
            {
                var lm = landmarks[i];
                // Explicitly use x, y, z floats
                _jobRawPositions[i] = new float3(lm.x, lm.y, lm.z);
                _jobTimestamps[i] = timestamp;
            }

            // 2. Schedule Job
            JobHandle handle;
            
            if (_filterType == LandmarkFilterManager.FilterType.OneEuro)
            {
                var job = new FilteringJobs.OneEuroFilterJob
                {
                    RawPositions = _jobRawPositions,
                    Timestamps = _jobTimestamps,
                    CurrentTime = timestamp,
                    MinCutoff = 1.0f, // TODO: Get from manager params
                    Beta = 0.007f,
                    DCutoff = 1.0f,
                    FilteredPositions = _jobFilteredPositions,
                    LastRawPositions = _jobLastRawPositions,
                    InternalState = _jobInternalState,
                    IsInitialized = _jobInitialized
                };
                handle = job.Schedule(count, 8); // Batch size 8
            }
            else // Kalman
            {
                var job = new FilteringJobs.KalmanFilterJob
                {
                    Measurement = _jobRawPositions,
                    DeltaTime = 0.016f, // Approximation
                    ProcessNoise = 1e-4f,
                    MeasurementNoise = 1e-2f,
                    State = _jobKalmanState,
                    Covariance = _jobCovariance,
                    IsInitialized = _jobInitialized,
                    Result = _jobFilteredPositions
                };
                handle = job.Schedule(count, 8);
            }

            // 3. Complete Job
            handle.Complete();

            // 4. Update Cache & Events
            if (!_cachedPositions.ContainsKey(0))
            {
                _cachedPositions[0] = new Vector3[MAX_JOB_LANDMARKS];
                _cachedVisibility[0] = new float[MAX_JOB_LANDMARKS];
            }

            for (int i = 0; i < count; i++)
            {
                float3 res = _jobFilteredPositions[i];
                _cachedPositions[0][i] = res;
                // use null coalescing for visibility since it's nullable
                _cachedVisibility[0][i] = landmarks[i].visibility ?? 0f;
            }
        }

        private void UpdatePerformanceStats()
        {
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccumulator > 0.5f)
            {
                _currentFPS = _fpsFrames / _fpsAccumulator;
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
            }
        }
        
        private void UpdateFiltering(IReadOnlyList<NormalizedLandmarks> poseLandmarks)
        {
            float timestamp = Time.time;
            const int MAX_LANDMARKS = 33;

            for (int poseIdx = 0; poseIdx < poseLandmarks.Count; poseIdx++)
            {
                var landmarks = poseLandmarks[poseIdx].landmarks;
                
                if (!_cachedPositions.ContainsKey(poseIdx))
                {
                    _cachedPositions[poseIdx] = new Vector3[MAX_LANDMARKS];
                    _cachedVisibility[poseIdx] = new float[MAX_LANDMARKS];
                }

                for (int i = 0; i < landmarks.Count && i < MAX_LANDMARKS; i++)
                {
                    var lm = landmarks[i];
                    Vector3 rawPos = new Vector3(lm.x, lm.y, lm.z);
                    float confidence = Mathf.Max(lm.visibility ?? 0f, lm.presence ?? 0f);

                    var state = _filterManager.FilterLandmark(poseIdx, i, rawPos, confidence, timestamp);
                    
                    _cachedPositions[poseIdx][i] = state.Position;
                    _cachedVisibility[poseIdx][i] = state.Confidence;

                    OnLandmarkUpdated?.Invoke(poseIdx, i, state);
                }
            }
        }

        #region Public API

        public void SetSkeletonVisible(bool visible)
        {
            _showSkeleton = visible;
            if (annotation != null) annotation.gameObject.SetActive(visible && _lastPoseDetected);
        }

        public void SetFilterType(LandmarkFilterManager.FilterType type) => _filterType = type;
        public void SetFilterPreset(LandmarkFilterManager.FilterPreset preset) => _filterPreset = preset;

        public int GetDetectedPoseCount()
        {
            lock (_currentTargetLock) return _currentTarget.poseLandmarks?.Count ?? 0;
        }

        public Vector2? GetLandmarkPosition(int poseIndex, int landmarkIndex)
        {
            if (_cachedPositions.ContainsKey(poseIndex) && landmarkIndex < 33)
            {
                Vector3 pos = _cachedPositions[poseIndex][landmarkIndex];
                float vis = _cachedVisibility[poseIndex][landmarkIndex];

                if (vis < _visibilityThreshold * 0.5f) return null;

                if (_rectTransform != null)
                {
                    float w = _rectTransform.rect.width;
                    float h = _rectTransform.rect.height;
                    return new Vector2((pos.x - 0.5f) * w, (0.5f - pos.y) * h);
                }
                return new Vector2(pos.x, pos.y);
            }
            return null;
        }

        public Vector3? GetSmoothedLandmark3D(int poseIndex, int landmarkIndex)
        {
            if (!_cachedPositions.ContainsKey(poseIndex) || landmarkIndex >= 33) return null;
            return _cachedPositions[poseIndex][landmarkIndex];
        }

        public (int visibleCount, int occludedCount, float avgConfidence) GetTrackingStats(int poseIndex = 0)
        {
            return _filterManager?.GetTrackingStats(poseIndex) ?? (0, 0, 0f);
        }
        
        public LandmarkFilterManager.FilterType GetCurrentFilterType() => _filterType;
        public float GetCurrentFPS() => _currentFPS;

        // Legacy Compatibility / Editor Utils
        public void SetOneEuroFilterEnabled(bool enabled)
        {
            _filterType = enabled ? LandmarkFilterManager.FilterType.OneEuro : LandmarkFilterManager.FilterType.Raw;
            _filterManager?.SetFilterType(_filterType);
        }

        public void SetOcclusionHandling(bool enabled)
        {
            _enableOcclusionPersistence = enabled;
            _filterManager?.SetOcclusionHandling(_enableOcclusionPersistence, _occlusionPersistenceFrames, _visibilityThreshold);
        }

        public void SetAdaptiveSmoothing(bool enabled)
        {
            _enableAdaptiveSmoothing = enabled;
            // Update logic if adaptive smoothing parameter exists in manager
            // For now just setting the bool as per legacy behavior
        }

        #endregion
    }
}
