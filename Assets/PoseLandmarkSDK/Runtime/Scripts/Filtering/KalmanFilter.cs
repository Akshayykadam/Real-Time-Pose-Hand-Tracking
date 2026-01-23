using UnityEngine;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Kalman Filter implementation for 3D monitoring of position, velocity, and acceleration.
    /// Provides robust estimation in noisy conditions (low light, fast movement).
    /// Supports axis-specific measurement noise for improved Z-axis (depth) handling.
    /// </summary>
    public class KalmanFilter
    {
        // Dimensions
        private const int STATE_DIM = 9; // x, y, z, vx, vy, vz, ax, ay, az
        private const int MEASURE_DIM = 3; // x, y, z measurements

        // Matrices
        private float[] _state = new float[STATE_DIM]; // State estimate
        private float[] _P = new float[STATE_DIM * STATE_DIM]; // Error covariance
        private float[] _Q = new float[STATE_DIM * STATE_DIM]; // Process noise
        private float[] _R = new float[MEASURE_DIM * MEASURE_DIM]; // Measurement noise
        private float[] _K = new float[STATE_DIM * MEASURE_DIM]; // Kalman gain

        private bool _initialized = false;
        private float _lastTime;

        // Configuration
        private float _processNoise = 1e-4f;
        private float _measurementNoise = 1e-2f;
        private float _zMeasurementNoiseMultiplier = 3.0f; // Z-axis is 3x noisier by default

        /// <summary>
        /// Create a Kalman filter with configurable noise parameters.
        /// </summary>
        /// <param name="processNoise">Process noise covariance</param>
        /// <param name="measurementNoise">Base measurement noise for X/Y axes</param>
        /// <param name="zNoiseMultiplier">Multiplier for Z-axis measurement noise (default: 3.0 = less trust in Z)</param>
        public KalmanFilter(float processNoise = 1e-4f, float measurementNoise = 1e-2f, float zNoiseMultiplier = 3.0f)
        {
            _processNoise = processNoise;
            _measurementNoise = measurementNoise;
            _zMeasurementNoiseMultiplier = zNoiseMultiplier;
            Reset();
        }

        public void Reset()
        {
            _initialized = false;
            
            // Initialize P (high uncertainty)
            System.Array.Clear(_P, 0, _P.Length);
            for (int i = 0; i < STATE_DIM; i++) _P[i * STATE_DIM + i] = 1.0f;

            // Initialize Q (process noise)
            System.Array.Clear(_Q, 0, _Q.Length);
            for (int i = 0; i < STATE_DIM; i++) _Q[i * STATE_DIM + i] = _processNoise;

            // Initialize R (measurement noise) - Axis-specific
            System.Array.Clear(_R, 0, _R.Length);
            _R[0] = _measurementNoise;                                    // X-axis
            _R[4] = _measurementNoise;                                    // Y-axis
            _R[8] = _measurementNoise * _zMeasurementNoiseMultiplier;     // Z-axis (higher noise = less trust)
        }
        
        /// <summary>
        /// Update the Z-axis measurement noise multiplier.
        /// Higher values mean less trust in Z measurements.
        /// </summary>
        public void SetZNoiseMultiplier(float multiplier)
        {
            _zMeasurementNoiseMultiplier = Mathf.Max(1.0f, multiplier);
            _R[8] = _measurementNoise * _zMeasurementNoiseMultiplier;
        }

        public Vector3 Update(Vector3 measurement, float timestamp)
        {
            if (!_initialized)
            {
                // First initialization
                _state[0] = measurement.x; _state[1] = measurement.y; _state[2] = measurement.z;
                _state[3] = 0; _state[4] = 0; _state[5] = 0; // Velocity
                _state[6] = 0; _state[7] = 0; _state[8] = 0; // Acceleration
                
                _lastTime = timestamp;
                _initialized = true;
                return measurement;
            }

            float dt = timestamp - _lastTime;
            if (dt <= 0) dt = 0.016f;
            _lastTime = timestamp;

            // --- Predict Step ---
            // F: State transition matrix
            // x = x + v*dt + 0.5*a*dt^2
            // v = v + a*dt
            // a = a
            
            // We do explicit matrix multiplication for performance optimization (3 dimensions interleaved)
            for (int dim = 0; dim < 3; dim++)
            {
                float pos = _state[dim];
                float vel = _state[dim + 3];
                float acc = _state[dim + 6];

                _state[dim] = pos + vel * dt + 0.5f * acc * dt * dt;
                _state[dim + 3] = vel + acc * dt;
                // _state[dim + 6] remains same (constant acceleration model)
            }

            // Update P = F*P*F' + Q
            // (Simplified diagonal update for performance)
            for (int i = 0; i < STATE_DIM; i++)
            {
                _P[i * STATE_DIM + i] += _Q[i * STATE_DIM + i] * dt;
            }


            // --- Update Step ---
            // Innovation y = z - Hx
            float[] y = new float[3];
            y[0] = measurement.x - _state[0];
            y[1] = measurement.y - _state[1];
            y[2] = measurement.z - _state[2];

            // Innovation covariance S = H*P*H' + R
            float[] S = new float[9];
            for (int i = 0; i < 3; i++)
                S[i * 3 + i] = _P[i * STATE_DIM + i] + _R[i * MEASURE_DIM + i];

            // Kalman Gain K = P*H'*inv(S)
            // H is just [I 0 0], so H' maps 3 measure dims to first 3 state dims
            // Simplified inverse for diagonal S
            for (int i = 0; i < 3; i++)
            {
                float invS = 1.0f / S[i * 3 + i];
                // K only affects first 3 columns since S is diagonal
                // And only rows correlated with position are updated
                // Full K calculation:
                for (int j = 0; j < STATE_DIM; j++)
                {
                    // Approximation for diagonal uncorrelated assumption
                    // K[j, i] approx P[j, i] * invS
                    // But with H=[I 0 0], only first 3 cols of P matter
                    if (j % 3 == i) // Match dimension x -> pos_x, vel_x, acc_x
                    {
                        int kIdx = j * MEASURE_DIM + i;
                        int pIdx = j * STATE_DIM + i; // P column corresponding to position
                        _K[kIdx] = _P[pIdx] * invS;
                    }
                }
            }

            // Update state x = x + K*y
            for (int i = 0; i < STATE_DIM; i++)
            {
                float change = 0;
                for (int j = 0; j < MEASURE_DIM; j++)
                {
                    change += _K[i * MEASURE_DIM + j] * y[j];
                }
                _state[i] += change;
            }

            // Update P = (I - K*H)*P
            for (int i = 0; i < STATE_DIM; i++)
            {
                 for (int j = 0; j < STATE_DIM; j++)
                 {
                     float khp = 0;
                     // Only first 3 cols of P matter because H is [I 0 0]
                     int measureIdx = i % 3; // Corresponds to x, y or z measurement
                     if (measureIdx < 3 && j % 3 == measureIdx) // Correlated terms
                     {
                         // K[i, measureIdx] * 1 * P[measureIdx, j]
                         // This is a simplified update to avoid full 9x9 matrix mult
                         float kVal = _K[i * MEASURE_DIM + measureIdx];
                         if (i < 3) // Position terms reduces uncertainty significantly
                            _P[i * STATE_DIM + j] -= kVal * _P[measureIdx * STATE_DIM + j];
                         else // Velocity/Accel updates
                            _P[i * STATE_DIM + j] *= 0.98f; // Decaying uncertainty
                     }
                 }
            }

            return new Vector3(_state[0], _state[1], _state[2]);
        }

        public Vector3 GetVelocity()
        {
            return new Vector3(_state[3], _state[4], _state[5]);
        }

        public Vector3 GetpredictedPosition(float futureTime)
        {
            float dt = futureTime;
            Vector3 pred = Vector3.zero;
            pred.x = _state[0] + _state[3] * dt + 0.5f * _state[6] * dt * dt;
            pred.y = _state[1] + _state[4] * dt + 0.5f * _state[7] * dt * dt;
            pred.z = _state[2] + _state[5] * dt + 0.5f * _state[8] * dt * dt;
            return pred;
        }
    }
}
