using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Controller for full body skeleton visualization using MediaPipe pose landmarks.
    /// Optimized for performance with frame skipping and landmark smoothing for stability.
    /// 
    /// Note: Smoothing is applied during GetLandmarkPosition() calls and visual display
    /// uses internal smoothing state rather than modifying MediaPipe's read-only structs.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FullBodySkeletonController : SimplePoseAnnotationController
    {
        [Header("Skeleton Settings")]
        [SerializeField] private float _visibilityThreshold = 0.3f;
        [SerializeField] private bool _showSkeleton = true;

        [Header("Performance")]
        [Tooltip("Skip N frames between draws. 0 = draw every frame, 1 = draw every other frame, etc.")]
        [SerializeField, Range(0, 5)] private int _frameSkip = 0;
        
        [Header("Stability / Smoothing")]
        [Tooltip("Enable landmark smoothing to reduce jitter (applied to GetLandmarkPosition results)")]
        [SerializeField] private bool _enableSmoothing = true;
        
        [Tooltip("Smoothing factor (0.0 = no smoothing/instant, 1.0 = maximum smoothing/very slow)")]
        [SerializeField, Range(0f, 0.95f)] private float _smoothingFactor = 0.6f;
        
        [Tooltip("Velocity smoothing to reduce sudden movements")]
        [SerializeField, Range(0f, 0.9f)] private float _velocitySmoothing = 0.4f;
        
        [Header("Reliability")]
        [Tooltip("Reject outliers that jump more than this distance (normalized coords, 0 = disabled)")]
        [SerializeField, Range(0f, 0.3f)] private float _outlierThreshold = 0.15f;
        
        [Tooltip("Weight smoothing by landmark confidence (more smoothing for low-confidence landmarks)")]
        [SerializeField] private bool _confidenceWeightedSmoothing = true;
        
        [Tooltip("Minimum consecutive frames needed before accepting a new landmark position")]
        [SerializeField, Range(1, 5)] private int _minConsecutiveFrames = 2;

        private RectTransform _rectTransform;
        private int _frameCounter = 0;
        private bool _lastPoseDetected = false;

        // Smoothing state - stores previous landmark positions per pose
        private const int MAX_LANDMARKS = 33;
        private Dictionary<int, Vector3[]> _smoothedPositions = new Dictionary<int, Vector3[]>();
        private Dictionary<int, Vector3[]> _velocities = new Dictionary<int, Vector3[]>();
        private Dictionary<int, float[]> _smoothedVisibility = new Dictionary<int, float[]>();
        private Dictionary<int, bool[]> _initialized = new Dictionary<int, bool[]>();
        private Dictionary<int, int[]> _consecutiveFrameCount = new Dictionary<int, int[]>();
        private Dictionary<int, Vector3[]> _pendingPositions = new Dictionary<int, Vector3[]>();

        // Cached values to avoid repeated allocations
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

            // Ensure proper stretching to fill parent
            _rectTransform.pivot = _stretchPivot;
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.anchoredPosition3D = _zeroPosition;

            // Ensure annotation is visible (not hidden)
            if (annotation != null)
            {
                annotation.gameObject.SetActive(_showSkeleton);
            }
        }

        protected override void SyncNow()
        {
            // Frame skipping for performance
            _frameCounter++;
            if (_frameSkip > 0 && (_frameCounter % (_frameSkip + 1)) != 0)
            {
                return;
            }

            lock (_currentTargetLock)
            {
                isStale = false;

                bool hasPose = _currentTarget.poseLandmarks != null && 
                               _currentTarget.poseLandmarks.Count > 0;

                // Early exit if skeleton is disabled
                if (!_showSkeleton)
                {
                    if (annotation != null && annotation.gameObject.activeSelf)
                    {
                        annotation.gameObject.SetActive(false);
                    }
                    return;
                }

                // Only toggle active state when detection state changes
                if (hasPose != _lastPoseDetected)
                {
                    _lastPoseDetected = hasPose;
                    if (annotation != null)
                    {
                        annotation.gameObject.SetActive(hasPose);
                    }
                    
                    // Reset smoothing state when pose detection changes
                    if (!hasPose)
                    {
                        _smoothedPositions.Clear();
                        _velocities.Clear();
                        _smoothedVisibility.Clear();
                        _initialized.Clear();
                    }
                }

                if (hasPose && annotation != null)
                {
                    // Update smoothing state
                    if (_enableSmoothing)
                    {
                        UpdateSmoothingState(_currentTarget.poseLandmarks);
                    }
                    
                    // Draw using original landmarks (MediaPipe handles visualization)
                    annotation.Draw(_currentTarget.poseLandmarks, false);
                }
            }
        }

        /// <summary>
        /// Update internal smoothing state from current landmarks
        /// Enhanced with outlier rejection and confidence-weighted smoothing
        /// </summary>
        private void UpdateSmoothingState(IReadOnlyList<NormalizedLandmarks> poseLandmarks)
        {
            float deltaTime = Time.deltaTime;
            float baseSmoothFactor = Mathf.Pow(_smoothingFactor, deltaTime * 60f);
            float velSmoothFactor = Mathf.Pow(_velocitySmoothing, deltaTime * 60f);

            for (int poseIdx = 0; poseIdx < poseLandmarks.Count; poseIdx++)
            {
                var landmarks = poseLandmarks[poseIdx].landmarks;
                
                // Ensure we have storage for this pose
                if (!_smoothedPositions.ContainsKey(poseIdx))
                {
                    _smoothedPositions[poseIdx] = new Vector3[MAX_LANDMARKS];
                    _velocities[poseIdx] = new Vector3[MAX_LANDMARKS];
                    _smoothedVisibility[poseIdx] = new float[MAX_LANDMARKS];
                    _initialized[poseIdx] = new bool[MAX_LANDMARKS];
                    _consecutiveFrameCount[poseIdx] = new int[MAX_LANDMARKS];
                    _pendingPositions[poseIdx] = new Vector3[MAX_LANDMARKS];
                }

                var smoothedPos = _smoothedPositions[poseIdx];
                var velocities = _velocities[poseIdx];
                var smoothedVis = _smoothedVisibility[poseIdx];
                var inited = _initialized[poseIdx];
                var frameCount = _consecutiveFrameCount[poseIdx];
                var pendingPos = _pendingPositions[poseIdx];

                for (int i = 0; i < landmarks.Count && i < MAX_LANDMARKS; i++)
                {
                    var lm = landmarks[i];
                    Vector3 currentPos = new Vector3(lm.x, lm.y, lm.z);
                    float currentVis = lm.visibility ?? 0f;
                    float currentPres = lm.presence ?? 0f;
                    float confidence = Mathf.Max(currentVis, currentPres);
                    
                    // Initialize if first time
                    if (!inited[i])
                    {
                        smoothedPos[i] = currentPos;
                        smoothedVis[i] = currentVis;
                        pendingPos[i] = currentPos;
                        frameCount[i] = 1;
                        inited[i] = true;
                        continue;
                    }
                    
                    // Calculate distance from current smoothed position
                    float distance = Vector3.Distance(currentPos, smoothedPos[i]);
                    
                    // Outlier rejection: if jump is too large, require consecutive frames
                    bool isOutlier = _outlierThreshold > 0 && distance > _outlierThreshold;
                    
                    if (isOutlier)
                    {
                        // Check if this is near the pending position (consistent outlier)
                        float pendingDistance = Vector3.Distance(currentPos, pendingPos[i]);
                        
                        if (pendingDistance < _outlierThreshold * 0.5f)
                        {
                            // Position is consistent with pending, increment counter
                            frameCount[i]++;
                            pendingPos[i] = Vector3.Lerp(pendingPos[i], currentPos, 0.5f);
                            
                            if (frameCount[i] >= _minConsecutiveFrames)
                            {
                                // Accept the new position after enough consistent frames
                                smoothedPos[i] = pendingPos[i];
                                frameCount[i] = 0;
                            }
                        }
                        else
                        {
                            // New outlier position, start fresh
                            pendingPos[i] = currentPos;
                            frameCount[i] = 1;
                        }
                        
                        // Don't update smoothed position yet for outliers
                        continue;
                    }
                    
                    // Reset frame counter for valid positions
                    frameCount[i] = 0;
                    
                    // Confidence-weighted smoothing: lower confidence = more smoothing
                    float smoothFactor = baseSmoothFactor;
                    if (_confidenceWeightedSmoothing)
                    {
                        // Scale smoothing based on confidence (low confidence = more smoothing)
                        float confWeight = Mathf.Clamp01(confidence);
                        smoothFactor = Mathf.Lerp(baseSmoothFactor * 1.5f, baseSmoothFactor * 0.5f, confWeight);
                        smoothFactor = Mathf.Clamp(smoothFactor, 0f, 0.98f);
                    }
                    
                    // Calculate velocity
                    Vector3 targetVelocity = (currentPos - smoothedPos[i]) / Mathf.Max(deltaTime, 0.001f);
                    velocities[i] = Vector3.Lerp(targetVelocity, velocities[i], velSmoothFactor);

                    // Apply smoothing with velocity prediction
                    Vector3 predictedPos = smoothedPos[i] + velocities[i] * deltaTime;
                    smoothedPos[i] = Vector3.Lerp(currentPos, predictedPos, smoothFactor * 0.5f);
                    smoothedPos[i] = Vector3.Lerp(currentPos, smoothedPos[i], smoothFactor);

                    // Smooth visibility
                    smoothedVis[i] = Mathf.Lerp(currentVis, smoothedVis[i], smoothFactor * 0.8f);
                }
            }
        }

        /// <summary>
        /// Toggle skeleton visibility at runtime
        /// </summary>
        public void SetSkeletonVisible(bool visible)
        {
            _showSkeleton = visible;
            if (annotation != null)
            {
                annotation.gameObject.SetActive(visible && _lastPoseDetected);
            }
        }

        /// <summary>
        /// Set frame skip value at runtime (0 = every frame, 1 = every 2nd frame, etc.)
        /// </summary>
        public void SetFrameSkip(int skipFrames)
        {
            _frameSkip = Mathf.Clamp(skipFrames, 0, 5);
        }

        /// <summary>
        /// Configure smoothing at runtime
        /// </summary>
        public void SetSmoothing(bool enabled, float factor = 0.5f, float velocityFactor = 0.3f)
        {
            _enableSmoothing = enabled;
            _smoothingFactor = Mathf.Clamp(factor, 0f, 0.95f);
            _velocitySmoothing = Mathf.Clamp(velocityFactor, 0f, 0.9f);
            
            // Reset smoothing state
            _smoothedPositions.Clear();
            _velocities.Clear();
            _smoothedVisibility.Clear();
            _initialized.Clear();
        }

        /// <summary>
        /// Returns the number of detected poses
        /// </summary>
        public int GetDetectedPoseCount()
        {
            lock (_currentTargetLock)
            {
                return _currentTarget.poseLandmarks?.Count ?? 0;
            }
        }

        /// <summary>
        /// Returns a specific landmark position for a given pose index.
        /// If smoothing is enabled, returns the smoothed position.
        /// </summary>
        public Vector2? GetLandmarkPosition(int poseIndex, int landmarkIndex)
        {
            lock (_currentTargetLock)
            {
                if (_currentTarget.poseLandmarks == null || 
                    poseIndex >= _currentTarget.poseLandmarks.Count)
                    return null;

                var landmarks = _currentTarget.poseLandmarks[poseIndex].landmarks;
                if (landmarkIndex >= landmarks.Count)
                    return null;

                var lm = landmarks[landmarkIndex];
                float vis = lm.visibility ?? 0f;
                float pres = lm.presence ?? 0f;
                
                if (vis < _visibilityThreshold && pres < _visibilityThreshold)
                    return null;

                // Use smoothed position if available and enabled
                float x = lm.x;
                float y = lm.y;
                
                if (_enableSmoothing && 
                    _smoothedPositions.ContainsKey(poseIndex) &&
                    _initialized.ContainsKey(poseIndex) &&
                    _initialized[poseIndex][landmarkIndex])
                {
                    var smoothed = _smoothedPositions[poseIndex];
                    x = smoothed[landmarkIndex].x;
                    y = smoothed[landmarkIndex].y;
                }

                // Convert normalized coords to screen coords
                if (_rectTransform != null)
                {
                    float w = _rectTransform.rect.width;
                    float h = _rectTransform.rect.height;
                    float screenX = (x - 0.5f) * w;
                    float screenY = (0.5f - y) * h;
                    return new Vector2(screenX, screenY);
                }

                return new Vector2(x, y);
            }
        }

        /// <summary>
        /// Returns a specific smoothed landmark in 3D (x, y, z normalized coordinates)
        /// </summary>
        public Vector3? GetSmoothedLandmark3D(int poseIndex, int landmarkIndex)
        {
            lock (_currentTargetLock)
            {
                if (!_enableSmoothing || 
                    !_smoothedPositions.ContainsKey(poseIndex) ||
                    !_initialized.ContainsKey(poseIndex))
                    return null;
                    
                if (landmarkIndex >= MAX_LANDMARKS || !_initialized[poseIndex][landmarkIndex])
                    return null;
                    
                return _smoothedPositions[poseIndex][landmarkIndex];
            }
        }
    }
}
