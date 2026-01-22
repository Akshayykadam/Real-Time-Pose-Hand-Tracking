using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Unity.Collections;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Applies low-light enhancement to the webcam feed for better pose detection
    /// in dark environments. Uses async GPU readback for zero frame drops.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class LowLightEnhancer : MonoBehaviour
    {
        [Header("Low Light Enhancement")]
        [Tooltip("Enable low-light enhancement processing")]
        [SerializeField] private bool _enabled = true;
        
        [Tooltip("Automatic adjustment based on average brightness")]
        [SerializeField] private bool _autoAdjust = true;
        
        [Header("Manual Settings")]
        [Tooltip("Brightness multiplier (1.0 = normal, >1.0 = brighter)")]
        [SerializeField, Range(0.5f, 2.5f)] private float _brightness = 1.2f;
        
        [Tooltip("Contrast adjustment (1.0 = normal, >1.0 = more contrast)")]
        [SerializeField, Range(0.5f, 2.0f)] private float _contrast = 1.1f;
        
        [Tooltip("Color saturation (1.0 = normal, 0 = grayscale)")]
        [SerializeField, Range(0f, 2.0f)] private float _saturation = 1.0f;
        
        [Tooltip("Gamma correction (lower = brighter shadows)")]
        [SerializeField, Range(0.3f, 2.0f)] private float _gamma = 0.9f;

        [Header("Advanced Enhancement")]
        [Tooltip("Enable local contrast enhancement (CLAHE-inspired)")]
        [SerializeField] private bool _enableLocalContrast = true;
        
        [Tooltip("Local contrast strength")]
        [SerializeField, Range(0f, 1f)] private float _localContrastStrength = 0.3f;
        
        [Tooltip("Noise reduction strength for low-light")]
        [SerializeField, Range(0f, 1f)] private float _noiseReduction = 0.2f;
        
        [Tooltip("Vignette correction (brighten edges)")]
        [SerializeField, Range(0f, 1f)] private float _vignetteCorrection = 0.1f;

        [Header("Auto-Adjust Settings")]
        [Tooltip("Target average brightness (0-1)")]
        [SerializeField, Range(0.2f, 0.6f)] private float _targetBrightness = 0.4f;
        
        [Tooltip("How quickly to adjust (lower = smoother)")]
        [SerializeField, Range(0.01f, 0.2f)] private float _adjustSpeed = 0.05f;
        
        [Tooltip("Sample interval in seconds")]
        [SerializeField, Range(0.05f, 0.5f)] private float _sampleInterval = 0.1f;

        private RawImage _rawImage;
        private Material _enhancementMaterial;
        private Material _originalMaterial;
        private Shader _enhancementShader;
        
        // Async GPU readback
        private bool _asyncReadbackPending = false;
        private float _lastSampleTime = 0f;
        private float _currentBrightness = 1.0f;
        private float _measuredBrightness = 0.5f;
        
        // Ring buffer for smooth brightness transitions
        private const int BRIGHTNESS_BUFFER_SIZE = 5;
        private float[] _brightnessBuffer = new float[BRIGHTNESS_BUFFER_SIZE];
        private int _brightnessBufferIndex = 0;
        private bool _brightnessBufferFilled = false;

        // Shader property IDs (cached for performance)
        private static readonly int _BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int _ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int _SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int _GammaId = Shader.PropertyToID("_Gamma");
        private static readonly int _LocalContrastId = Shader.PropertyToID("_LocalContrast");
        private static readonly int _NoiseReductionId = Shader.PropertyToID("_NoiseReduction");
        private static readonly int _VignetteCorrectionId = Shader.PropertyToID("_VignetteCorrection");

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            
            // Load the enhanced shader
            _enhancementShader = Shader.Find("PoseLandmarkSDK/LowLightEnhancementV2");
            if (_enhancementShader == null)
            {
                // Fall back to original shader
                _enhancementShader = Shader.Find("PoseLandmarkSDK/LowLightEnhancement");
            }
            
            if (_enhancementShader == null)
            {
                Debug.LogWarning("[LowLightEnhancer] Shader not found. Low-light enhancement disabled.");
                enabled = false;
                return;
            }
            
            // Create enhancement material
            _enhancementMaterial = new Material(_enhancementShader);
            _originalMaterial = _rawImage.material;
            
            // Initialize brightness buffer
            for (int i = 0; i < BRIGHTNESS_BUFFER_SIZE; i++)
            {
                _brightnessBuffer[i] = 0.5f;
            }
        }

        private void OnEnable()
        {
            if (_enabled && _enhancementMaterial != null)
            {
                _rawImage.material = _enhancementMaterial;
                UpdateMaterialProperties();
            }
        }

        private void OnDisable()
        {
            if (_rawImage != null && _originalMaterial != null)
            {
                _rawImage.material = _originalMaterial;
            }
        }

        private void Update()
        {
            if (!_enabled || _enhancementMaterial == null) return;

            if (_autoAdjust)
            {
                TryStartAsyncBrightnessSample();
            }
            
            UpdateMaterialProperties();
        }

        /// <summary>
        /// Start async brightness sampling using GPU readback (no frame drops)
        /// </summary>
        private void TryStartAsyncBrightnessSample()
        {
            // Check interval and ensure we're not already waiting
            if (_asyncReadbackPending || Time.time - _lastSampleTime < _sampleInterval)
                return;

            if (_rawImage.texture == null)
                return;

            RenderTexture rt = _rawImage.texture as RenderTexture;
            if (rt == null)
                return;

            _lastSampleTime = Time.time;
            _asyncReadbackPending = true;

            // Request async readback of a small portion of the texture
            int sampleSize = Mathf.Min(16, rt.width, rt.height);
            int x = (rt.width - sampleSize) / 2;
            int y = (rt.height - sampleSize) / 2;

            AsyncGPUReadback.Request(rt, 0, x, sampleSize, y, sampleSize, 0, 1, OnAsyncReadbackComplete);
        }

        /// <summary>
        /// Callback when async GPU readback completes
        /// </summary>
        private void OnAsyncReadbackComplete(AsyncGPUReadbackRequest request)
        {
            _asyncReadbackPending = false;

            if (request.hasError)
            {
                return;
            }

            try
            {
                // Get the data as Color32 (most common format)
                if (request.hasError) return;
                var pixels = request.GetData<Color32>();
                if (pixels.IsCreated)
                {
                    float totalBrightness = 0f;
                    int count = pixels.Length;

                    // Sample every 4th pixel for performance
                    int step = Mathf.Max(1, count / 64);
                    int sampledCount = 0;

                    for (int i = 0; i < count; i += step)
                    {
                        Color32 pixel = pixels[i];
                        // ITU-R BT.709 luminance formula
                        float brightness = (pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f) / 255f;
                        totalBrightness += brightness;
                        sampledCount++;
                    }

                    if (sampledCount > 0)
                    {
                        _measuredBrightness = totalBrightness / sampledCount;
                        
                        // Add to ring buffer
                        _brightnessBuffer[_brightnessBufferIndex] = _measuredBrightness;
                        _brightnessBufferIndex = (_brightnessBufferIndex + 1) % BRIGHTNESS_BUFFER_SIZE;
                        if (_brightnessBufferIndex == 0) _brightnessBufferFilled = true;

                        // Calculate smoothed average
                        float smoothedBrightness = CalculateSmoothedBrightness();
                        
                        // Adjust multiplier to reach target
                        if (smoothedBrightness > 0.01f)
                        {
                            float targetMultiplier = _targetBrightness / smoothedBrightness;
                            targetMultiplier = Mathf.Clamp(targetMultiplier, 0.8f, 2.5f);
                            _currentBrightness = Mathf.Lerp(_currentBrightness, targetMultiplier, _adjustSpeed);
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Silently handle any errors during readback processing
            }
        }

        /// <summary>
        /// Calculate average from ring buffer for smooth transitions
        /// </summary>
        private float CalculateSmoothedBrightness()
        {
            int count = _brightnessBufferFilled ? BRIGHTNESS_BUFFER_SIZE : _brightnessBufferIndex;
            if (count == 0) return 0.5f;

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                sum += _brightnessBuffer[i];
            }
            return sum / count;
        }

        private void UpdateMaterialProperties()
        {
            if (_enhancementMaterial == null) return;

            float finalBrightness = _autoAdjust ? _currentBrightness : _brightness;
            
            _enhancementMaterial.SetFloat(_BrightnessId, finalBrightness);
            _enhancementMaterial.SetFloat(_ContrastId, _contrast);
            _enhancementMaterial.SetFloat(_SaturationId, _saturation);
            _enhancementMaterial.SetFloat(_GammaId, _gamma);
            
            // Advanced properties (V2 shader)
            if (_enhancementMaterial.HasProperty(_LocalContrastId))
            {
                _enhancementMaterial.SetFloat(_LocalContrastId, _enableLocalContrast ? _localContrastStrength : 0f);
                _enhancementMaterial.SetFloat(_NoiseReductionId, _noiseReduction);
                _enhancementMaterial.SetFloat(_VignetteCorrectionId, _vignetteCorrection);
            }
        }

        private void OnDestroy()
        {
            if (_enhancementMaterial != null)
            {
                Destroy(_enhancementMaterial);
            }
        }

        #region Public API

        /// <summary>
        /// Enable/disable low-light enhancement at runtime
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (_rawImage != null)
            {
                _rawImage.material = enabled ? _enhancementMaterial : _originalMaterial;
            }
        }

        /// <summary>
        /// Check if enhancement is active
        /// </summary>
        public bool IsEnabled => _enabled && _enhancementMaterial != null;

        /// <summary>
        /// Get current measured brightness (0-1)
        /// </summary>
        public float MeasuredBrightness => _measuredBrightness;

        /// <summary>
        /// Get current brightness multiplier being applied
        /// </summary>
        public float CurrentBrightnessMultiplier => _currentBrightness;

        /// <summary>
        /// Manually set brightness multiplier
        /// </summary>
        public void SetBrightness(float brightness)
        {
            _brightness = Mathf.Clamp(brightness, 0.5f, 2.5f);
            _autoAdjust = false;
        }

        /// <summary>
        /// Enable automatic brightness adjustment
        /// </summary>
        public void EnableAutoAdjust(bool enable, float targetBrightness = 0.4f)
        {
            _autoAdjust = enable;
            _targetBrightness = targetBrightness;
        }

        /// <summary>
        /// Configure advanced enhancement settings
        /// </summary>
        public void SetAdvancedSettings(float localContrast = 0.3f, float noiseReduction = 0.2f, float vignetteCorrection = 0.1f)
        {
            _localContrastStrength = Mathf.Clamp01(localContrast);
            _noiseReduction = Mathf.Clamp01(noiseReduction);
            _vignetteCorrection = Mathf.Clamp01(vignetteCorrection);
        }

        #endregion
    }
}
