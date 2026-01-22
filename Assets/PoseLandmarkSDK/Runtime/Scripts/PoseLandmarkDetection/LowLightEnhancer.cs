using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Applies low-light enhancement to the webcam feed for better pose detection
    /// in dark environments. Attach this to the same GameObject as the RawImage
    /// displaying the webcam feed.
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

        [Header("Auto-Adjust Settings")]
        [Tooltip("Target average brightness (0-1)")]
        [SerializeField, Range(0.2f, 0.6f)] private float _targetBrightness = 0.4f;
        
        [Tooltip("How quickly to adjust (lower = smoother)")]
        [SerializeField, Range(0.01f, 0.2f)] private float _adjustSpeed = 0.05f;

        private RawImage _rawImage;
        private Material _enhancementMaterial;
        private Material _originalMaterial;
        private Shader _enhancementShader;
        private Texture2D _sampleTexture;
        private float _currentBrightness = 1.0f;
        private float _frameCounter = 0f;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            
            // Load the shader
            _enhancementShader = Shader.Find("PoseLandmarkSDK/LowLightEnhancement");
            if (_enhancementShader == null)
            {
                Debug.LogWarning("LowLightEnhancement shader not found. Low-light enhancement disabled.");
                enabled = false;
                return;
            }
            
            // Create enhancement material
            _enhancementMaterial = new Material(_enhancementShader);
            _originalMaterial = _rawImage.material;
            
            // Create small texture for brightness sampling
            _sampleTexture = new Texture2D(8, 8, TextureFormat.RGB24, false);
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
                AutoAdjustBrightness();
            }
            
            UpdateMaterialProperties();
        }

        private void AutoAdjustBrightness()
        {
            // Sample brightness every few frames to save performance
            _frameCounter += Time.deltaTime;
            if (_frameCounter < 0.1f) return;
            _frameCounter = 0f;

            if (_rawImage.texture == null) return;

            // Estimate average brightness from texture
            float avgBrightness = EstimateAverageBrightness();
            
            // Adjust brightness to reach target
            if (avgBrightness > 0.01f)
            {
                float targetMultiplier = _targetBrightness / avgBrightness;
                targetMultiplier = Mathf.Clamp(targetMultiplier, 0.8f, 2.5f);
                
                _currentBrightness = Mathf.Lerp(_currentBrightness, targetMultiplier, _adjustSpeed);
            }
        }

        private float EstimateAverageBrightness()
        {
            // Simple estimation using GPU readback (can be expensive)
            // For production, consider using compute shaders or async readback
            
            RenderTexture rt = _rawImage.texture as RenderTexture;
            if (rt == null) return 0.5f;

            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = rt;
            
            // Sample a small region from center
            int sampleSize = 8;
            int x = (rt.width - sampleSize) / 2;
            int y = (rt.height - sampleSize) / 2;
            
            try
            {
                _sampleTexture.ReadPixels(new UnityEngine.Rect(x, y, sampleSize, sampleSize), 0, 0);
                _sampleTexture.Apply();
            }
            catch
            {
                RenderTexture.active = currentRT;
                return 0.5f;
            }
            
            RenderTexture.active = currentRT;

            // Calculate average brightness
            UnityEngine.Color[] pixels = _sampleTexture.GetPixels();
            float totalBrightness = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                UnityEngine.Color pixel = pixels[i];
                totalBrightness += (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f);
            }
            
            return totalBrightness / pixels.Length;
        }

        private void UpdateMaterialProperties()
        {
            if (_enhancementMaterial == null) return;

            float finalBrightness = _autoAdjust ? _currentBrightness : _brightness;
            
            _enhancementMaterial.SetFloat("_Brightness", finalBrightness);
            _enhancementMaterial.SetFloat("_Contrast", _contrast);
            _enhancementMaterial.SetFloat("_Saturation", _saturation);
            _enhancementMaterial.SetFloat("_Gamma", _gamma);
        }

        private void OnDestroy()
        {
            if (_enhancementMaterial != null)
            {
                Destroy(_enhancementMaterial);
            }
            if (_sampleTexture != null)
            {
                Destroy(_sampleTexture);
            }
        }

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
    }
}
