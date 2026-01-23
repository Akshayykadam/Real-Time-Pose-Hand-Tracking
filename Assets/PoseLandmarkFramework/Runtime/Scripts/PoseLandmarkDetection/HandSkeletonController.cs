using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Use explicit type alias to avoid conflicts
using TaskNormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;
using TaskNormalizedLandmarks = Mediapipe.Tasks.Components.Containers.NormalizedLandmarks;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Controller for hand skeleton visualization using MediaPipe hand landmarks.
    /// Features: 21 landmark points per hand, parallel Job System filtering, adaptive FPS-based skipping.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HandSkeletonController : SimpleHandAnnotationController
    {
        /// <summary>
        /// Hand Landmark indices for MediaPipe 21-point hand model.
        /// </summary>
        public enum HandLandmark
        {
            WRIST = 0,
            THUMB_CMC = 1,
            THUMB_MCP = 2,
            THUMB_IP = 3,
            THUMB_TIP = 4,
            INDEX_FINGER_MCP = 5,
            INDEX_FINGER_PIP = 6,
            INDEX_FINGER_DIP = 7,
            INDEX_FINGER_TIP = 8,
            MIDDLE_FINGER_MCP = 9,
            MIDDLE_FINGER_PIP = 10,
            MIDDLE_FINGER_DIP = 11,
            MIDDLE_FINGER_TIP = 12,
            RING_FINGER_MCP = 13,
            RING_FINGER_PIP = 14,
            RING_FINGER_DIP = 15,
            RING_FINGER_TIP = 16,
            PINKY_MCP = 17,
            PINKY_PIP = 18,
            PINKY_DIP = 19,
            PINKY_TIP = 20
        }

        [Header("Skeleton Settings")]
        [SerializeField] private float _visibilityThreshold = 0.3f;
        [SerializeField] private bool _showSkeleton = true;

        [Header("Performance")]
        [Tooltip("Target FPS for processing. 0 = Use screen limit.")]
        [SerializeField] private int _targetFPS = 60;
        
        [Tooltip("Automatically skip frames if FPS drops below 30")]
        [SerializeField] private bool _adaptiveFrameSkipping = true;
        
        [Tooltip("Use Unity Job System for filtering (Recommended for multiple hands)")]
        [SerializeField] private bool _useJobSystem = true;
        
        [Header("Filtering Mode")]
        [Tooltip("Type of filter to use for jitter reduction")]
        [SerializeField] private LandmarkFilterManager.FilterType _filterType = LandmarkFilterManager.FilterType.OneEuro;
        
        [Tooltip("Preset for One-Euro filter")]
        [SerializeField] private LandmarkFilterManager.FilterPreset _filterPreset = LandmarkFilterManager.FilterPreset.Smooth;
        
        [Header("Smoothing Parameters")]
        [Tooltip("Additional smoothing factor (0 = no extra smoothing, 1 = maximum smoothing)")]
        [SerializeField, Range(0f, 0.95f)] private float _smoothingFactor = 0.7f;
        
        [Tooltip("One-Euro filter: Lower = more smoothing, less responsive")]
        [SerializeField, Range(0.1f, 5f)] private float _minCutoff = 0.5f;
        
        [Tooltip("One-Euro filter: Higher = faster response to quick movements")]
        [SerializeField, Range(0.001f, 0.1f)] private float _beta = 0.005f;
        
        [Tooltip("One-Euro filter: Derivative cutoff")]
        [SerializeField, Range(0.5f, 2f)] private float _dCutoff = 1.0f;
        
        [Header("Occlusion & Prediction")]
        [SerializeField] private bool _enableOcclusionPersistence = true;
        [SerializeField, Range(1, 60)] private int _occlusionPersistenceFrames = 15;
        
        [Header("Adaptive Smoothing")]
        [SerializeField] private bool _enableAdaptiveSmoothing = true;
        [SerializeField, Range(0.1f, 2f)] private float _fastMovementThreshold = 0.5f;

        private RectTransform _rectTransform;
        private int _frameSkip = 0;
        private int _frameCounter = 0;
        private bool _lastHandDetected = false;

        // Managers
        private LandmarkFilterManager _filterManager;
        
        // Performance Monitoring
        private float _fpsAccumulator = 0f;
        private int _fpsFrames = 0;
        private float _currentFPS = 60f;
        
        // Job System Buffers for parallel processing
        // 21 landmarks per hand, support up to 2 hands
        private const int MAX_JOB_LANDMARKS = 21;
        private const int MAX_HANDS = 2;
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
        public event System.Action<bool> OnHandDetectionChanged;

        // Cache containers for smoothed positions (used by API)
        private Dictionary<int, Vector3[]> _cachedPositions = new Dictionary<int, Vector3[]>();
        private Dictionary<int, Vector3[]> _smoothedPositions = new Dictionary<int, Vector3[]>();
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

            // Initialize Filter Manager with smooth preset for hands
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
            
            int totalLandmarks = MAX_JOB_LANDMARKS * MAX_HANDS;
            
            _jobRawPositions = new NativeArray<float3>(totalLandmarks, Allocator.Persistent);
            _jobTimestamps = new NativeArray<float>(totalLandmarks, Allocator.Persistent);
            _jobFilteredPositions = new NativeArray<float3>(totalLandmarks, Allocator.Persistent);
            _jobInitialized = new NativeArray<bool>(totalLandmarks, Allocator.Persistent);
            _jobLastRawPositions = new NativeArray<float3>(totalLandmarks, Allocator.Persistent);
            
            // OneEuro needs 6 floats per landmark [val, deriv] * 3 dims
            _jobInternalState = new NativeArray<float>(totalLandmarks * 6, Allocator.Persistent);
            
            // Kalman needs 9 state + 9 covariance per landmark
            _jobKalmanState = new NativeArray<float>(totalLandmarks * 9, Allocator.Persistent);
            _jobCovariance = new NativeArray<float>(totalLandmarks * 9, Allocator.Persistent);
            
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
            // Adaptive Frame Skipping
            UpdatePerformanceStats();
            if (_adaptiveFrameSkipping)
            {
                if (_currentFPS < 30f) _frameSkip = 2;
                else if (_currentFPS < 45f) _frameSkip = 1;
                else _frameSkip = 0;
            }
            
            _frameCounter++;
            if (_frameSkip > 0 && (_frameCounter % (_frameSkip + 1)) != 0) return;

            lock (_currentTargetLock)
            {
                isStale = false;
                bool hasHand = _currentTarget.handLandmarks != null && _currentTarget.handLandmarks.Count > 0;

                if (!_showSkeleton)
                {
                    if (annotation != null && annotation.gameObject.activeSelf) 
                        annotation.gameObject.SetActive(false);
                    return;
                }

                if (hasHand != _lastHandDetected)
                {
                    _lastHandDetected = hasHand;
                    if (annotation != null) annotation.gameObject.SetActive(hasHand);
                    OnHandDetectionChanged?.Invoke(hasHand);
                    
                    if (!hasHand)
                    {
                        _filterManager.Reset();
                        _smoothedPositions.Clear();
                        if (_useJobSystem && _jobsInitialized)
                        {
                            int total = MAX_JOB_LANDMARKS * MAX_HANDS;
                            for(int i = 0; i < total; i++) _jobInitialized[i] = false;
                        }
                    }
                }

                if (hasHand && annotation != null)
                {
                    // Process filtering for each hand
                    for (int handIdx = 0; handIdx < _currentTarget.handLandmarks.Count && handIdx < MAX_HANDS; handIdx++)
                    {
                        var rawLandmarks = _currentTarget.handLandmarks[handIdx].landmarks;
                        
                        if (_useJobSystem && _jobsInitialized)
                        {
                            ProcessFilteringJobs(handIdx, rawLandmarks);
                        }
                        else
                        {
                            UpdateFiltering(handIdx, rawLandmarks);
                        }
                        
                        // Apply additional exponential smoothing for API consumers
                        ApplyExponentialSmoothing(handIdx);
                    }
                    
                    // Set handedness for coloring
                    annotation.SetHandedness(_currentTarget.handedness);
                    
                    // Draw the raw landmarks - filtering is applied to cached positions for API use
                    annotation.Draw(_currentTarget.handLandmarks, false);
                }
            }
        }
        
        private void ApplyExponentialSmoothing(int handIndex)
        {
            if (!_cachedPositions.ContainsKey(handIndex)) return;
            
            if (!_smoothedPositions.ContainsKey(handIndex))
            {
                _smoothedPositions[handIndex] = new Vector3[MAX_JOB_LANDMARKS];
                // Initialize with current positions
                for (int i = 0; i < MAX_JOB_LANDMARKS; i++)
                {
                    _smoothedPositions[handIndex][i] = _cachedPositions[handIndex][i];
                }
                return;
            }
            
            // Apply exponential moving average
            float alpha = 1f - _smoothingFactor;
            for (int i = 0; i < MAX_JOB_LANDMARKS; i++)
            {
                Vector3 current = _cachedPositions[handIndex][i];
                Vector3 previous = _smoothedPositions[handIndex][i];
                _smoothedPositions[handIndex][i] = Vector3.Lerp(previous, current, alpha);
            }
        }
        
        private void ProcessFilteringJobs(int handIndex, List<TaskNormalizedLandmark> landmarks)
        {
            float timestamp = Time.time;
            int count = Mathf.Min(landmarks.Count, MAX_JOB_LANDMARKS);
            int offset = handIndex * MAX_JOB_LANDMARKS;

            // Prepare Input Data
            for (int i = 0; i < count; i++)
            {
                var lm = landmarks[i];
                _jobRawPositions[offset + i] = new float3(lm.x, lm.y, lm.z);
                _jobTimestamps[offset + i] = timestamp;
            }

            // Schedule Job
            JobHandle handle;
            
            if (_filterType == LandmarkFilterManager.FilterType.OneEuro)
            {
                var job = new FilteringJobs.OneEuroFilterJob
                {
                    RawPositions = _jobRawPositions,
                    Timestamps = _jobTimestamps,
                    CurrentTime = timestamp,
                    MinCutoff = _minCutoff,
                    Beta = _beta,
                    DCutoff = _dCutoff,
                    FilteredPositions = _jobFilteredPositions,
                    LastRawPositions = _jobLastRawPositions,
                    InternalState = _jobInternalState,
                    IsInitialized = _jobInitialized
                };
                handle = job.Schedule(count, 8);
            }
            else // Kalman
            {
                var job = new FilteringJobs.KalmanFilterJob
                {
                    Measurement = _jobRawPositions,
                    DeltaTime = Time.deltaTime,
                    ProcessNoise = 1e-5f,  // Lower = more smoothing
                    MeasurementNoise = 5e-2f,  // Higher = more smoothing
                    State = _jobKalmanState,
                    Covariance = _jobCovariance,
                    IsInitialized = _jobInitialized,
                    Result = _jobFilteredPositions
                };
                handle = job.Schedule(count, 8);
            }

            handle.Complete();

            // Update Cache
            if (!_cachedPositions.ContainsKey(handIndex))
            {
                _cachedPositions[handIndex] = new Vector3[MAX_JOB_LANDMARKS];
                _cachedVisibility[handIndex] = new float[MAX_JOB_LANDMARKS];
            }

            for (int i = 0; i < count; i++)
            {
                float3 res = _jobFilteredPositions[offset + i];
                _cachedPositions[handIndex][i] = res;
                _cachedVisibility[handIndex][i] = landmarks[i].visibility ?? 0f;
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
        
        private void UpdateFiltering(int handIndex, List<TaskNormalizedLandmark> landmarks)
        {
            float timestamp = Time.time;
            
            if (!_cachedPositions.ContainsKey(handIndex))
            {
                _cachedPositions[handIndex] = new Vector3[MAX_JOB_LANDMARKS];
                _cachedVisibility[handIndex] = new float[MAX_JOB_LANDMARKS];
            }

            for (int i = 0; i < landmarks.Count && i < MAX_JOB_LANDMARKS; i++)
            {
                var lm = landmarks[i];
                Vector3 rawPos = new Vector3(lm.x, lm.y, lm.z);
                float confidence = Mathf.Max(lm.visibility ?? 0f, lm.presence ?? 0f);

                var state = _filterManager.FilterLandmark(handIndex, i, rawPos, confidence, timestamp);
                
                _cachedPositions[handIndex][i] = state.Position;
                _cachedVisibility[handIndex][i] = state.Confidence;

                OnLandmarkUpdated?.Invoke(handIndex, i, state);
            }
        }

        #region Public API

        /// <summary>
        /// Show or hide the hand skeleton visualization.
        /// </summary>
        public void SetSkeletonVisible(bool visible)
        {
            _showSkeleton = visible;
            if (annotation != null) annotation.gameObject.SetActive(visible && _lastHandDetected);
        }

        public void SetFilterType(LandmarkFilterManager.FilterType type) => _filterType = type;
        public void SetFilterPreset(LandmarkFilterManager.FilterPreset preset) => _filterPreset = preset;
        
        /// <summary>
        /// Set smoothing factor. Higher = more smoothing, slower response.
        /// </summary>
        public void SetSmoothingFactor(float factor)
        {
            _smoothingFactor = Mathf.Clamp(factor, 0f, 0.95f);
        }
        
        /// <summary>
        /// Set One-Euro filter parameters for fine-tuning.
        /// </summary>
        public void SetOneEuroParameters(float minCutoff, float beta, float dCutoff)
        {
            _minCutoff = Mathf.Clamp(minCutoff, 0.1f, 5f);
            _beta = Mathf.Clamp(beta, 0.001f, 0.1f);
            _dCutoff = Mathf.Clamp(dCutoff, 0.5f, 2f);
        }

        /// <summary>
        /// Get the number of currently detected hands.
        /// </summary>
        public int GetDetectedHandCount()
        {
            lock (_currentTargetLock) return _currentTarget.handLandmarks?.Count ?? 0;
        }

        /// <summary>
        /// Get 2D position of a specific hand landmark in screen space.
        /// Uses smoothed positions for stable output.
        /// </summary>
        /// <param name="handIndex">Hand index (0 or 1)</param>
        /// <param name="landmarkIndex">Landmark index (0-20) or use HandLandmark enum</param>
        public Vector2? GetLandmarkPosition(int handIndex, int landmarkIndex)
        {
            // Use smoothed positions for API calls
            Vector3[] positions = _smoothedPositions.ContainsKey(handIndex) 
                ? _smoothedPositions[handIndex] 
                : _cachedPositions.ContainsKey(handIndex) ? _cachedPositions[handIndex] : null;
                
            if (positions != null && landmarkIndex < MAX_JOB_LANDMARKS)
            {
                Vector3 pos = positions[landmarkIndex];
                float vis = _cachedVisibility.ContainsKey(handIndex) ? _cachedVisibility[handIndex][landmarkIndex] : 1f;

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

        /// <summary>
        /// Get 2D position of a specific hand landmark using the enum.
        /// </summary>
        public Vector2? GetLandmarkPosition(int handIndex, HandLandmark landmark)
        {
            return GetLandmarkPosition(handIndex, (int)landmark);
        }

        /// <summary>
        /// Get 3D position of a specific hand landmark (smoothed).
        /// </summary>
        public Vector3? GetSmoothedLandmark3D(int handIndex, int landmarkIndex)
        {
            Vector3[] positions = _smoothedPositions.ContainsKey(handIndex) 
                ? _smoothedPositions[handIndex] 
                : _cachedPositions.ContainsKey(handIndex) ? _cachedPositions[handIndex] : null;
                
            if (positions == null || landmarkIndex >= MAX_JOB_LANDMARKS) return null;
            return positions[landmarkIndex];
        }

        /// <summary>
        /// Get fingertip position for a specific finger.
        /// </summary>
        public Vector2? GetFingertipPosition(int handIndex, int fingerIndex)
        {
            // Fingertip indices: Thumb=4, Index=8, Middle=12, Ring=16, Pinky=20
            int[] fingertipIndices = { 4, 8, 12, 16, 20 };
            if (fingerIndex < 0 || fingerIndex >= fingertipIndices.Length) return null;
            return GetLandmarkPosition(handIndex, fingertipIndices[fingerIndex]);
        }

        /// <summary>
        /// Check if a specific finger is extended (pointing up).
        /// </summary>
        public bool IsFingerExtended(int handIndex, int fingerIndex)
        {
            Vector3[] positions = _smoothedPositions.ContainsKey(handIndex) 
                ? _smoothedPositions[handIndex] 
                : _cachedPositions.ContainsKey(handIndex) ? _cachedPositions[handIndex] : null;
                
            if (positions == null) return false;
            
            // MCP and TIP indices for each finger
            int[] mcpIndices = { 1, 5, 9, 13, 17 };  // Thumb uses CMC
            int[] tipIndices = { 4, 8, 12, 16, 20 };
            
            if (fingerIndex < 0 || fingerIndex >= 5) return false;
            
            Vector3 mcp = positions[mcpIndices[fingerIndex]];
            Vector3 tip = positions[tipIndices[fingerIndex]];
            
            // Finger is extended if tip is above (lower y in normalized coords) MCP
            return tip.y < mcp.y;
        }

        public LandmarkFilterManager.FilterType GetCurrentFilterType() => _filterType;
        public float GetCurrentFPS() => _currentFPS;
        public float GetSmoothingFactor() => _smoothingFactor;
        
        /// <summary>
        /// Get tracking statistics for a specific hand.
        /// </summary>
        public (int visibleCount, int occludedCount, float avgConfidence) GetTrackingStats(int handIndex = 0)
        {
            return _filterManager?.GetTrackingStats(handIndex) ?? (0, 0, 0f);
        }

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
        }

        #endregion
    }
}
