using UnityEngine;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Implementation of the 1€ (One-Euro) Filter for jitter reduction.
    /// Reference: http://cristal.univ-lille.fr/~casiez/1euro/
    /// 
    /// Key characteristics:
    /// - Low latency during slow movements (precise positioning)
    /// - Smooth transitions during fast movements (no jitter amplification)
    /// - Adaptive cutoff frequency based on velocity
    /// </summary>
    public class OneEuroFilter
    {
        // Filter parameters
        private float _minCutoff;   // Minimum cutoff frequency (Hz). Lower = more smoothing when slow
        private float _beta;        // Speed coefficient. Higher = less lag when speed increases
        private float _dCutoff;     // Derivative cutoff frequency (Hz). For velocity estimation

        // Internal state
        private LowPassFilter _xFilter;
        private LowPassFilter _dxFilter;
        private float _lastValue;
        private float _lastTime;
        private bool _initialized;

        /// <summary>
        /// Create a new 1€ filter with default parameters
        /// </summary>
        /// <param name="minCutoff">Minimum cutoff frequency in Hz (default: 1.0). Lower = more smoothing at low speeds</param>
        /// <param name="beta">Speed coefficient (default: 0.007). Higher = more responsiveness at high speeds</param>
        /// <param name="dCutoff">Derivative cutoff in Hz (default: 1.0). For velocity estimation smoothing</param>
        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.007f, float dCutoff = 1.0f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dCutoff = dCutoff;
            _xFilter = new LowPassFilter(ComputeAlpha(1.0f, _minCutoff));
            _dxFilter = new LowPassFilter(ComputeAlpha(1.0f, _dCutoff));
            _initialized = false;
        }

        /// <summary>
        /// Filter the input value
        /// </summary>
        /// <param name="value">Raw input value</param>
        /// <param name="timestamp">Current timestamp in seconds</param>
        /// <returns>Filtered value</returns>
        public float Filter(float value, float timestamp)
        {
            if (!_initialized)
            {
                _initialized = true;
                _lastValue = value;
                _lastTime = timestamp;
                _xFilter.SetAlpha(ComputeAlpha(1.0f, _minCutoff));
                _xFilter.Reset(value);
                _dxFilter.Reset(0f);
                return value;
            }

            // Compute dt
            float dt = timestamp - _lastTime;
            if (dt <= 0f) dt = 1f / 60f; // Fallback to 60 fps

            // Estimate velocity
            float dx = (value - _lastValue) / dt;
            float edx = _dxFilter.Filter(dx, ComputeAlpha(dt, _dCutoff));

            // Compute dynamic cutoff based on speed
            float cutoff = _minCutoff + _beta * Mathf.Abs(edx);

            // Filter the value
            float result = _xFilter.Filter(value, ComputeAlpha(dt, cutoff));

            _lastValue = value;
            _lastTime = timestamp;

            return result;
        }

        /// <summary>
        /// Reset the filter state
        /// </summary>
        public void Reset()
        {
            _initialized = false;
        }

        /// <summary>
        /// Reset to a specific value
        /// </summary>
        public void Reset(float value)
        {
            _initialized = true;
            _lastValue = value;
            _lastTime = Time.time;
            _xFilter.Reset(value);
            _dxFilter.Reset(0f);
        }

        /// <summary>
        /// Update filter parameters at runtime
        /// </summary>
        public void SetParameters(float minCutoff, float beta, float dCutoff = 1.0f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dCutoff = dCutoff;
        }

        private float ComputeAlpha(float dt, float cutoff)
        {
            float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
            return 1.0f / (1.0f + tau / dt);
        }

        /// <summary>
        /// Simple exponential low-pass filter
        /// </summary>
        private class LowPassFilter
        {
            private float _y;
            private float _alpha;
            private bool _initialized;

            public LowPassFilter(float alpha)
            {
                _alpha = alpha;
                _initialized = false;
            }

            public void SetAlpha(float alpha)
            {
                _alpha = Mathf.Clamp01(alpha);
            }

            public float Filter(float value, float alpha)
            {
                SetAlpha(alpha);
                if (!_initialized)
                {
                    _initialized = true;
                    _y = value;
                }
                else
                {
                    _y = _alpha * value + (1f - _alpha) * _y;
                }
                return _y;
            }

            public void Reset(float value)
            {
                _initialized = true;
                _y = value;
            }
        }
    }

    /// <summary>
    /// 3D version of the One-Euro filter for landmark positions
    /// </summary>
    public class OneEuroFilter3D
    {
        private OneEuroFilter _xFilter;
        private OneEuroFilter _yFilter;
        private OneEuroFilter _zFilter;

        public OneEuroFilter3D(float minCutoff = 1.0f, float beta = 0.007f, float dCutoff = 1.0f)
        {
            _xFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
            _yFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
            _zFilter = new OneEuroFilter(minCutoff, beta, dCutoff);
        }

        public Vector3 Filter(Vector3 value, float timestamp)
        {
            return new Vector3(
                _xFilter.Filter(value.x, timestamp),
                _yFilter.Filter(value.y, timestamp),
                _zFilter.Filter(value.z, timestamp)
            );
        }

        public void Reset()
        {
            _xFilter.Reset();
            _yFilter.Reset();
            _zFilter.Reset();
        }

        public void Reset(Vector3 value)
        {
            _xFilter.Reset(value.x);
            _yFilter.Reset(value.y);
            _zFilter.Reset(value.z);
        }

        public void SetParameters(float minCutoff, float beta, float dCutoff = 1.0f)
        {
            _xFilter.SetParameters(minCutoff, beta, dCutoff);
            _yFilter.SetParameters(minCutoff, beta, dCutoff);
            _zFilter.SetParameters(minCutoff, beta, dCutoff);
        }
    }
}
