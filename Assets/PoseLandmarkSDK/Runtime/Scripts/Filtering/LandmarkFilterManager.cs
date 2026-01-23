using System.Collections.Generic;
using UnityEngine;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Manages filters for all pose landmarks.
    /// Supports One-Euro Filter, Kalman Filter, and Trajectory Prediction for occlusion.
    /// </summary>
    public class LandmarkFilterManager
    {
        public enum FilterType
        {
            OneEuro,
            Kalman,
            Raw
        }

        public enum FilterPreset
        {
            Responsive,
            Balanced,
            Smooth,
            VerySmooth
        }

        /// <summary>
        /// State of a single landmark's tracking
        /// </summary>
        public struct LandmarkState
        {
            public Vector3 Position;
            public float Visibility;
            public bool IsOccluded;
            public int OccludedFrameCount;
            public float Confidence;
            public bool IsPredicted; // True if position is hypothetical (prediction)
        }

        // Filter indices
        private const int MAX_LANDMARKS = 33;

        // Managers
        private Dictionary<int, OneEuroFilter3D[]> _oneEuroFilters = new Dictionary<int, OneEuroFilter3D[]>();
        private Dictionary<int, KalmanFilter[]> _kalmanFilters = new Dictionary<int, KalmanFilter[]>();
        private Dictionary<int, LandmarkState[]> _states = new Dictionary<int, LandmarkState[]>();
        private PosePredictor _predictor = new PosePredictor();
        private ZAxisStabilizer _zAxisStabilizer = new ZAxisStabilizer();
        
        // Configuration
        private FilterType _activeFilterType = FilterType.OneEuro;
        private float _visibilityThreshold = 0.3f;
        private int _maxOcclusionFrames = 10;
        private bool _enableOcclusionPersistence = true;
        private bool _enableZAxisStabilization = true;

        // Current parameters
        private float _minCutoff = 1.0f;
        private float _beta = 0.007f;
        private float _dCutoff = 1.0f;
        
        // One-Euro Presets
        private static readonly Dictionary<FilterPreset, (float minCutoff, float beta, float dCutoff)> PresetParams = 
            new Dictionary<FilterPreset, (float, float, float)>
            {
                { FilterPreset.Responsive, (3.0f, 0.05f, 1.0f) },
                { FilterPreset.Balanced, (1.5f, 0.007f, 1.0f) },
                { FilterPreset.Smooth, (0.8f, 0.004f, 1.0f) },
                { FilterPreset.VerySmooth, (0.4f, 0.001f, 1.0f) }
            };

        public LandmarkFilterManager(FilterPreset preset = FilterPreset.Balanced)
        {
            SetPreset(preset);
        }

        public void SetFilterType(FilterType type)
        {
            _activeFilterType = type;
            Reset(); // Reset history when switching algos
        }

        public void SetPreset(FilterPreset preset)
        {
            var (minCutoff, beta, dCutoff) = PresetParams[preset];
            _minCutoff = minCutoff;
            _beta = beta;
            _dCutoff = dCutoff;

            UpdateFilterParameters();
        }

        public void SetOcclusionHandling(bool enabled, int maxFrames = 10, float visibilityThreshold = 0.3f)
        {
            _enableOcclusionPersistence = enabled;
            _maxOcclusionFrames = maxFrames;
            _visibilityThreshold = visibilityThreshold;
        }
        
        /// <summary>
        /// Configure Z-axis stabilization settings.
        /// </summary>
        public void SetZAxisStabilization(bool enabled, bool useConfidenceWeighting = true, 
                                          bool useRelativeAnchoring = true, bool useAnatomicalConstraints = true,
                                          float zScaleFactor = 1.0f, int slidingWindowSize = 5)
        {
            _enableZAxisStabilization = enabled;
            _zAxisStabilizer.UseConfidenceWeighting = useConfidenceWeighting;
            _zAxisStabilizer.UseRelativeAnchoring = useRelativeAnchoring;
            _zAxisStabilizer.UseAnatomicalConstraints = useAnatomicalConstraints;
            _zAxisStabilizer.ZScaleFactor = zScaleFactor;
            _zAxisStabilizer.SlidingWindowSize = slidingWindowSize;
        }
        
        /// <summary>
        /// Get the Z-axis stabilizer for direct configuration.
        /// </summary>
        public ZAxisStabilizer GetZAxisStabilizer() => _zAxisStabilizer;

        public LandmarkState FilterLandmark(int poseIndex, int landmarkIndex, Vector3 rawPosition, 
            float visibility, float timestamp)
        {
            EnsureStorage(poseIndex);

            var prevState = _states[poseIndex][landmarkIndex];
            var newState = new LandmarkState();

            bool isVisible = visibility >= _visibilityThreshold;

            if (isVisible)
            {
                // -- Visible: Update Filters & Predictor --
                
                // 1. Update Predictor History
                _predictor.Update(poseIndex, landmarkIndex, rawPosition, timestamp);

                // 2. Apply Active Filter
                // 2. Apply Active Filter
                if (_activeFilterType == FilterType.OneEuro)
                {
                    newState.Position = _oneEuroFilters[poseIndex][landmarkIndex].Filter(rawPosition, timestamp);
                }
                else if (_activeFilterType == FilterType.Kalman)
                {
                    newState.Position = _kalmanFilters[poseIndex][landmarkIndex].Update(rawPosition, timestamp);
                }
                else
                {
                    // Raw/Passthrough
                    newState.Position = rawPosition;
                }

                newState.Visibility = visibility;
                newState.IsOccluded = false;
                newState.OccludedFrameCount = 0;
                newState.Confidence = visibility;
                newState.IsPredicted = false;
            }
            else
            {
                // -- Occluded: Use Prediction or Persistence --
                
                if (_enableOcclusionPersistence && prevState.OccludedFrameCount < _maxOcclusionFrames)
                {
                    // Use Prediction if available, otherwise hold last position
                    Vector3 predictedPos = _predictor.Predict(poseIndex, landmarkIndex, timestamp);
                    
                    if (predictedPos != Vector3.zero)
                    {
                        newState.Position = predictedPos;
                        newState.IsPredicted = true;
                    }
                    else
                    {
                        newState.Position = prevState.Position; // Fallback to persistence
                        newState.IsPredicted = false;
                    }

                    newState.Visibility = prevState.Visibility * 0.95f; // Decay visibility
                    newState.IsOccluded = true;
                    newState.OccludedFrameCount = prevState.OccludedFrameCount + 1;
                    newState.Confidence = Mathf.Max(0, prevState.Confidence - 0.1f);
                }
                else
                {
                    // Landmark truly lost (exceeded persistence window)
                    newState.Position = rawPosition;
                    newState.Visibility = visibility;
                    newState.IsOccluded = true;
                    newState.OccludedFrameCount = _maxOcclusionFrames;
                    newState.Confidence = 0f;
                    newState.IsPredicted = false;
                    
                    // Reset filters for this landmark
                    _oneEuroFilters[poseIndex][landmarkIndex].Reset();
                    _kalmanFilters[poseIndex][landmarkIndex].Reset();
                }
            }

            _states[poseIndex][landmarkIndex] = newState;
            return newState;
        }
        
        private void EnsureStorage(int poseIndex)
        {
            if (!_oneEuroFilters.ContainsKey(poseIndex))
            {
                _oneEuroFilters[poseIndex] = new OneEuroFilter3D[MAX_LANDMARKS];
                _kalmanFilters[poseIndex] = new KalmanFilter[MAX_LANDMARKS];
                _states[poseIndex] = new LandmarkState[MAX_LANDMARKS];

                for (int i = 0; i < MAX_LANDMARKS; i++)
                {
                    _oneEuroFilters[poseIndex][i] = new OneEuroFilter3D(_minCutoff, _beta, _dCutoff);
                    _kalmanFilters[poseIndex][i] = new KalmanFilter(); // Default noise params
                }
            }
        }
        
        private void UpdateFilterParameters()
        {
            foreach (var filters in _oneEuroFilters.Values)
            {
                foreach (var filter in filters)
                {
                    filter.SetParameters(_minCutoff, _beta, _dCutoff);
                }
            }
        }

        public void Reset()
        {
            foreach (var filters in _oneEuroFilters.Values)
                foreach (var f in filters) f.Reset();
                
            foreach (var filters in _kalmanFilters.Values)
                foreach (var f in filters) f.Reset();
                
            _states.Clear();
            _predictor = new PosePredictor();
            _zAxisStabilizer.Reset();
        }
        
        /// <summary>
        /// Apply Z-axis stabilization to all landmarks for a pose.
        /// Should be called after filtering all landmarks.
        /// </summary>
        public void StabilizeZAxis(int poseIndex)
        {
            if (!_enableZAxisStabilization || !_states.ContainsKey(poseIndex)) return;
            
            // Extract positions and visibilities
            Vector3[] positions = new Vector3[MAX_LANDMARKS];
            float[] visibilities = new float[MAX_LANDMARKS];
            
            for (int i = 0; i < MAX_LANDMARKS; i++)
            {
                positions[i] = _states[poseIndex][i].Position;
                visibilities[i] = _states[poseIndex][i].Visibility;
            }
            
            // Apply Z-axis stabilization
            _zAxisStabilizer.Stabilize(poseIndex, positions, visibilities);
            
            // Write back stabilized positions
            for (int i = 0; i < MAX_LANDMARKS; i++)
            {
                var state = _states[poseIndex][i];
                state.Position = positions[i];
                _states[poseIndex][i] = state;
            }
        }

        public (int visibleCount, int occludedCount, float avgConfidence) GetTrackingStats(int poseIndex)
        {
            if (!_states.ContainsKey(poseIndex)) return (0, 0, 0f);

            int visible = 0;
            int occluded = 0;
            float totalConf = 0f;

            for (int i = 0; i < MAX_LANDMARKS; i++)
            {
                var state = _states[poseIndex][i];
                if (state.Confidence > 0.01f)
                {
                    if (state.IsOccluded) occluded++;
                    else visible++;
                    totalConf += state.Confidence;
                }
            }
            return (visible, occluded, totalConf / Mathf.Max(1, visible + occluded));
        }
        
        /// <summary>
        /// Get the current filtered state for a specific landmark.
        /// Returns null if the pose/landmark is not tracked.
        /// </summary>
        public LandmarkState? GetFilteredState(int poseIndex, int landmarkIndex)
        {
            if (!_states.ContainsKey(poseIndex) || landmarkIndex >= MAX_LANDMARKS || landmarkIndex < 0)
                return null;
            
            return _states[poseIndex][landmarkIndex];
        }
    }
}
