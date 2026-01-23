using System.Collections.Generic;
using UnityEngine;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Advanced Z-axis (depth) stabilization for pose landmarks.
    /// Implements multiple strategies to improve the inherently noisy depth estimates from 2D pose detection.
    /// 
    /// Strategies:
    /// 1. Multi-frame temporal averaging
    /// 2. Confidence-weighted Z values
    /// 3. Relative Z anchoring to hip center
    /// 4. Anatomical bone length constraints
    /// </summary>
    public class ZAxisStabilizer
    {
        #region Configuration
        
        /// <summary>
        /// Number of frames to average Z values over
        /// </summary>
        public int SlidingWindowSize { get; set; } = 5;
        
        /// <summary>
        /// Enable confidence-weighted Z blending (low visibility = less trust)
        /// </summary>
        public bool UseConfidenceWeighting { get; set; } = true;
        
        /// <summary>
        /// Enable relative Z anchoring to hip center
        /// </summary>
        public bool UseRelativeAnchoring { get; set; } = true;
        
        /// <summary>
        /// Enable anatomical bone length constraints
        /// </summary>
        public bool UseAnatomicalConstraints { get; set; } = true;
        
        /// <summary>
        /// Strength of anatomical constraint correction (0-1)
        /// </summary>
        public float AnatomicalConstraintStrength { get; set; } = 0.7f;
        
        /// <summary>
        /// Multiplier for relative Z offsets from hip center
        /// Higher values preserve more depth variation, lower values flatten
        /// </summary>
        public float ZScaleFactor { get; set; } = 1.0f;
        
        #endregion

        #region Internal State
        
        private const int MAX_LANDMARKS = 33;
        
        // Sliding window history for each pose and landmark
        private Dictionary<int, Queue<float>[]> _zHistory = new Dictionary<int, Queue<float>[]>();
        
        // Previous Z values for confidence blending
        private Dictionary<int, float[]> _previousZ = new Dictionary<int, float[]>();
        
        // Calibrated bone length ratios (normalized to hip-to-shoulder distance)
        private float _calibratedTorsoLength = 0.0f;
        private bool _isCalibrated = false;
        
        #endregion

        #region MediaPipe Landmark Indices
        
        // Body landmarks
        private const int NOSE = 0;
        private const int LEFT_SHOULDER = 11;
        private const int RIGHT_SHOULDER = 12;
        private const int LEFT_ELBOW = 13;
        private const int RIGHT_ELBOW = 14;
        private const int LEFT_WRIST = 15;
        private const int RIGHT_WRIST = 16;
        private const int LEFT_HIP = 23;
        private const int RIGHT_HIP = 24;
        private const int LEFT_KNEE = 25;
        private const int RIGHT_KNEE = 26;
        private const int LEFT_ANKLE = 27;
        private const int RIGHT_ANKLE = 28;
        
        // Standard body proportions (relative to torso length)
        private static readonly float UPPER_ARM_RATIO = 0.45f;
        private static readonly float LOWER_ARM_RATIO = 0.40f;
        private static readonly float UPPER_LEG_RATIO = 0.65f;
        private static readonly float LOWER_LEG_RATIO = 0.60f;
        
        #endregion

        #region Public API
        
        /// <summary>
        /// Stabilize Z-axis values for all landmarks of a pose.
        /// Should be called after primary filtering.
        /// </summary>
        /// <param name="poseIndex">Index of the pose being tracked</param>
        /// <param name="positions">Array of landmark positions (will be modified in place)</param>
        /// <param name="visibilities">Array of landmark visibility scores</param>
        public void Stabilize(int poseIndex, Vector3[] positions, float[] visibilities)
        {
            if (positions == null || positions.Length == 0) return;
            
            EnsureStorage(poseIndex);
            
            // 1. Apply multi-frame averaging
            ApplyTemporalSmoothing(poseIndex, positions);
            
            // 2. Apply confidence-weighted blending
            if (UseConfidenceWeighting)
            {
                ApplyConfidenceWeighting(poseIndex, positions, visibilities);
            }
            
            // 3. Apply relative anchoring
            if (UseRelativeAnchoring)
            {
                ApplyRelativeAnchoring(poseIndex, positions);
            }
            
            // 4. Apply anatomical constraints
            if (UseAnatomicalConstraints)
            {
                ApplyAnatomicalConstraints(positions, visibilities);
            }
            
            // Store for next frame
            for (int i = 0; i < positions.Length && i < MAX_LANDMARKS; i++)
            {
                _previousZ[poseIndex][i] = positions[i].z;
            }
        }
        
        /// <summary>
        /// Stabilize a single landmark's Z value.
        /// Useful when processing landmarks individually.
        /// </summary>
        public float StabilizeZ(int poseIndex, int landmarkIndex, float rawZ, float visibility)
        {
            EnsureStorage(poseIndex);
            
            // Temporal averaging
            var history = _zHistory[poseIndex][landmarkIndex];
            history.Enqueue(rawZ);
            while (history.Count > SlidingWindowSize)
                history.Dequeue();
            
            float averagedZ = 0f;
            foreach (float z in history)
                averagedZ += z;
            averagedZ /= history.Count;
            
            // Confidence weighting
            if (UseConfidenceWeighting)
            {
                float weight = Mathf.Clamp01(visibility * 2.0f); // Scale visibility [0-0.5] to [0-1]
                float prevZ = _previousZ[poseIndex][landmarkIndex];
                averagedZ = Mathf.Lerp(prevZ, averagedZ, weight);
            }
            
            _previousZ[poseIndex][landmarkIndex] = averagedZ;
            return averagedZ;
        }
        
        /// <summary>
        /// Calibrate bone lengths from current pose.
        /// Call this when the user is in a neutral T-pose for best results.
        /// </summary>
        public void CalibrateFromPose(Vector3[] positions)
        {
            if (positions.Length < 25) return;
            
            Vector3 leftHip = positions[LEFT_HIP];
            Vector3 rightHip = positions[RIGHT_HIP];
            Vector3 leftShoulder = positions[LEFT_SHOULDER];
            Vector3 rightShoulder = positions[RIGHT_SHOULDER];
            
            Vector3 hipCenter = (leftHip + rightHip) * 0.5f;
            Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
            
            _calibratedTorsoLength = Vector3.Distance(hipCenter, shoulderCenter);
            _isCalibrated = _calibratedTorsoLength > 0.01f;
            
            Debug.Log($"[ZAxisStabilizer] Calibrated torso length: {_calibratedTorsoLength:F4}");
        }
        
        /// <summary>
        /// Reset all history and calibration data.
        /// </summary>
        public void Reset()
        {
            _zHistory.Clear();
            _previousZ.Clear();
            _calibratedTorsoLength = 0f;
            _isCalibrated = false;
        }
        
        #endregion

        #region Private Methods
        
        private void EnsureStorage(int poseIndex)
        {
            if (!_zHistory.ContainsKey(poseIndex))
            {
                _zHistory[poseIndex] = new Queue<float>[MAX_LANDMARKS];
                _previousZ[poseIndex] = new float[MAX_LANDMARKS];
                
                for (int i = 0; i < MAX_LANDMARKS; i++)
                {
                    _zHistory[poseIndex][i] = new Queue<float>();
                    _previousZ[poseIndex][i] = 0f;
                }
            }
        }
        
        /// <summary>
        /// Apply sliding window temporal averaging to Z values.
        /// </summary>
        private void ApplyTemporalSmoothing(int poseIndex, Vector3[] positions)
        {
            for (int i = 0; i < positions.Length && i < MAX_LANDMARKS; i++)
            {
                var history = _zHistory[poseIndex][i];
                history.Enqueue(positions[i].z);
                while (history.Count > SlidingWindowSize)
                    history.Dequeue();
                
                // Calculate average
                float sum = 0f;
                foreach (float z in history)
                    sum += z;
                
                positions[i].z = sum / history.Count;
            }
        }
        
        /// <summary>
        /// Blend Z values based on visibility confidence.
        /// Low visibility means less trust in the new Z value.
        /// </summary>
        private void ApplyConfidenceWeighting(int poseIndex, Vector3[] positions, float[] visibilities)
        {
            for (int i = 0; i < positions.Length && i < MAX_LANDMARKS; i++)
            {
                // Scale visibility to usable range and clamp
                float visibility = (visibilities != null && i < visibilities.Length) ? visibilities[i] : 0.5f;
                float weight = Mathf.Clamp01(visibility * 2.0f);
                
                float previousZ = _previousZ[poseIndex][i];
                float currentZ = positions[i].z;
                
                // Blend: more visibility = more trust in new value
                positions[i].z = Mathf.Lerp(previousZ, currentZ, weight);
            }
        }
        
        /// <summary>
        /// Express Z as relative offsets from hip center.
        /// This stabilizes overall depth while preserving limb depth variations.
        /// </summary>
        private void ApplyRelativeAnchoring(int poseIndex, Vector3[] positions)
        {
            if (positions.Length <= RIGHT_HIP) return;
            
            // Calculate hip center as reference
            Vector3 leftHip = positions[LEFT_HIP];
            Vector3 rightHip = positions[RIGHT_HIP];
            float referenceZ = (leftHip.z + rightHip.z) * 0.5f;
            
            // Apply relative Z with scale factor
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == LEFT_HIP || i == RIGHT_HIP) continue; // Keep hips as anchors
                
                float offset = positions[i].z - referenceZ;
                positions[i].z = referenceZ + (offset * ZScaleFactor);
            }
        }
        
        /// <summary>
        /// Apply anatomical bone length constraints.
        /// Uses expected limb proportions to correct obviously wrong Z values.
        /// </summary>
        private void ApplyAnatomicalConstraints(Vector3[] positions, float[] visibilities)
        {
            if (positions.Length <= RIGHT_ANKLE) return;
            if (!_isCalibrated)
            {
                // Auto-calibrate on first run
                CalibrateFromPose(positions);
                if (!_isCalibrated) return;
            }
            
            float strength = AnatomicalConstraintStrength;
            
            // Constrain upper arms (shoulder to elbow)
            ConstrainBone(positions, visibilities, LEFT_SHOULDER, LEFT_ELBOW, 
                         _calibratedTorsoLength * UPPER_ARM_RATIO, strength);
            ConstrainBone(positions, visibilities, RIGHT_SHOULDER, RIGHT_ELBOW, 
                         _calibratedTorsoLength * UPPER_ARM_RATIO, strength);
            
            // Constrain lower arms (elbow to wrist)
            ConstrainBone(positions, visibilities, LEFT_ELBOW, LEFT_WRIST, 
                         _calibratedTorsoLength * LOWER_ARM_RATIO, strength);
            ConstrainBone(positions, visibilities, RIGHT_ELBOW, RIGHT_WRIST, 
                         _calibratedTorsoLength * LOWER_ARM_RATIO, strength);
            
            // Constrain upper legs (hip to knee)
            ConstrainBone(positions, visibilities, LEFT_HIP, LEFT_KNEE, 
                         _calibratedTorsoLength * UPPER_LEG_RATIO, strength);
            ConstrainBone(positions, visibilities, RIGHT_HIP, RIGHT_KNEE, 
                         _calibratedTorsoLength * UPPER_LEG_RATIO, strength);
            
            // Constrain lower legs (knee to ankle)
            ConstrainBone(positions, visibilities, LEFT_KNEE, LEFT_ANKLE, 
                         _calibratedTorsoLength * LOWER_LEG_RATIO, strength);
            ConstrainBone(positions, visibilities, RIGHT_KNEE, RIGHT_ANKLE, 
                         _calibratedTorsoLength * LOWER_LEG_RATIO, strength);
        }
        
        /// <summary>
        /// Constrain a bone to its expected length by adjusting the child joint's Z.
        /// </summary>
        private void ConstrainBone(Vector3[] positions, float[] visibilities, 
                                   int parentIdx, int childIdx, float expectedLength, float strength)
        {
            // Skip if either joint has low visibility
            float parentVis = (visibilities != null && parentIdx < visibilities.Length) ? visibilities[parentIdx] : 0.5f;
            float childVis = (visibilities != null && childIdx < visibilities.Length) ? visibilities[childIdx] : 0.5f;
            
            if (parentVis < 0.3f || childVis < 0.3f) return;
            
            Vector3 parent = positions[parentIdx];
            Vector3 child = positions[childIdx];
            
            float actualLength = Vector3.Distance(parent, child);
            if (actualLength < 0.001f) return;
            
            // Calculate how far off we are
            float lengthRatio = expectedLength / actualLength;
            
            // Only correct if significantly off (>20% error)
            if (Mathf.Abs(lengthRatio - 1.0f) < 0.2f) return;
            
            // Calculate corrected Z (primarily adjust Z as it's the noisy dimension)
            Vector3 direction = (child - parent).normalized;
            Vector3 correctedChild = parent + direction * expectedLength;
            
            // Blend with original based on strength and confidence in child visibility
            float blendFactor = strength * childVis;
            positions[childIdx].z = Mathf.Lerp(child.z, correctedChild.z, blendFactor);
        }
        
        #endregion
    }
}
