using System.Collections.Generic;
using UnityEngine;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Predicts future landmark positions based on recent trajectory.
    /// Used for handling long-term occlusions (>10 frames) where simple persistence fails.
    /// </summary>
    public class PosePredictor
    {
        private const int HISTORY_SIZE = 5;
        private const int MAX_PREDICTION_FRAMES = 60; // Max 1 second at 60fps

        // History buffer for each landmark: [poseIndex][landmarkIndex] -> CircularBuffer
        private Dictionary<int, Dictionary<int, CircularBuffer<Vector3>>> _history = 
            new Dictionary<int, Dictionary<int, CircularBuffer<Vector3>>>();

        // Prediction state
        private Dictionary<int, Dictionary<int, PredictionState>> _predictionStates = 
            new Dictionary<int, Dictionary<int, PredictionState>>();

        struct PredictionState
        {
            public Vector3 Velocity;
            public Vector3 Acceleration;
            public Vector3 LastValidPosition;
            public float LastValidTime;
            public bool IsPredicting;
        }

        /// <summary>
        /// Update predictor with new valid data
        /// </summary>
        public void Update(int poseIndex, int landmarkIndex, Vector3 position, float timestamp)
        {
            EnsureStorage(poseIndex, landmarkIndex);

            var buffer = _history[poseIndex][landmarkIndex];
            buffer.Add(position, timestamp);

            // Update Prediction State
            if (buffer.Count >= 3)
            {
                // Calculate kinematics from history
                Vector3 p0 = buffer.Get(0); // Newest
                Vector3 p1 = buffer.Get(1);
                Vector3 p2 = buffer.Get(2); // Oldest
                
                float dt1 = buffer.GetTime(0) - buffer.GetTime(1);
                float dt2 = buffer.GetTime(1) - buffer.GetTime(2);
                
                if (dt1 > 0.001f && dt2 > 0.001f)
                {
                    Vector3 v0 = (p0 - p1) / dt1;
                    Vector3 v1 = (p1 - p2) / dt2;
                    Vector3 a = (v0 - v1) / ((dt1 + dt2) * 0.5f);

                    var state = new PredictionState
                    {
                        Velocity = v0,
                        Acceleration = a,
                        LastValidPosition = p0,
                        LastValidTime = timestamp,
                        IsPredicting = false
                    };
                    _predictionStates[poseIndex][landmarkIndex] = state;
                }
            }
        }

        /// <summary>
        /// Get predicted position for a lost landmark
        /// </summary>
        public Vector3 Predict(int poseIndex, int landmarkIndex, float currentTimestamp)
        {
            if (!_predictionStates.ContainsKey(poseIndex) || 
                !_predictionStates[poseIndex].ContainsKey(landmarkIndex))
            {
                return Vector3.zero;
            }

            var state = _predictionStates[poseIndex][landmarkIndex];
            float dt = currentTimestamp - state.LastValidTime;

            // Limit prediction time to avoid runaway values
            if (dt > 1.0f) // Max 1 second prediction
            {
                return state.LastValidPosition + state.Velocity * 1.0f; // Linear fallback cap
            }

            // Kinematic prediction: p = p0 + v*t + 0.5*a*t^2
            // Dampen acceleration over time to stabilize
            float damping = Mathf.Exp(-dt * 2.0f); 
            Vector3 prediction = state.LastValidPosition + 
                               state.Velocity * dt + 
                               0.5f * state.Acceleration * dt * dt * damping;

            return prediction;
        }

        /// <summary>
        /// Reset predictor for a specific pose
        /// </summary>
        public void Reset(int poseIndex)
        {
            if (_history.ContainsKey(poseIndex)) _history[poseIndex].Clear();
            if (_predictionStates.ContainsKey(poseIndex)) _predictionStates[poseIndex].Clear();
        }

        private void EnsureStorage(int poseIndex, int landmarkIndex)
        {
            if (!_history.ContainsKey(poseIndex))
            {
                _history[poseIndex] = new Dictionary<int, CircularBuffer<Vector3>>();
                _predictionStates[poseIndex] = new Dictionary<int, PredictionState>();
            }
            
            if (!_history[poseIndex].ContainsKey(landmarkIndex))
            {
                _history[poseIndex][landmarkIndex] = new CircularBuffer<Vector3>(HISTORY_SIZE);
            }
        }

        // Helper class for history buffer
        private class CircularBuffer<T>
        {
            private T[] _buffer;
            private float[] _timestamps;
            private int _head;
            private int _count;

            public int Count => _count;

            public CircularBuffer(int capacity)
            {
                _buffer = new T[capacity];
                _timestamps = new float[capacity];
                _head = 0;
                _count = 0;
            }

            public void Add(T item, float timestamp)
            {
                _head = (_head - 1 + _buffer.Length) % _buffer.Length;
                _buffer[_head] = item;
                _timestamps[_head] = timestamp;
                if (_count < _buffer.Length) _count++;
            }

            public T Get(int index)
            {
                if (index >= _count) return default;
                return _buffer[(_head + index) % _buffer.Length];
            }
            
            public float GetTime(int index)
            {
                 if (index >= _count) return 0f;
                 return _timestamps[(_head + index) % _buffer.Length];
            }
        }
    }
}
