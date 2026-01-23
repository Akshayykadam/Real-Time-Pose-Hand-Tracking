using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityColor = UnityEngine.Color;
using UIImage = UnityEngine.UI.Image;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Advanced diagnostics UI for detailed tracking analysis.
    /// Visualizes per-landmark confidence, tracking quality, and filtering status.
    /// </summary>
    public class AdvancedPoseDiagnostics : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FullBodySkeletonController _skeletonController;
        [SerializeField] private LowLightEnhancer _lowLightEnhancer;
        
        [Header("UI Components")]
        [SerializeField] private Text _statsText;
        [SerializeField] private Text _qualityText;
        [SerializeField] private RectTransform _overlayContainer;
        [SerializeField] private GameObject _landmarkDotPrefab;
        
        [Header("Visualization Settings")]
        [SerializeField] private bool _showPerLandmarkConfidence = true;
        [SerializeField] private bool _showOcclusionWarning = true;
        [SerializeField] private float _dotScale = 1.0f;
        
        [Header("Colors")]
        [SerializeField] private UnityColor _goodColor = new UnityColor(0.2f, 0.8f, 0.2f, 0.8f);
        [SerializeField] private UnityColor _warningColor = new UnityColor(1f, 0.8f, 0f, 0.8f);
        [SerializeField] private UnityColor _badColor = new UnityColor(1f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private UnityColor _occludedColor = new UnityColor(0.5f, 0.5f, 0.5f, 0.5f);

        // State
        private List<UIImage> _landmarkDots = new List<UIImage>();
        private float _updateInterval = 0.1f;
        private float _lastUpdate = 0f;
        
        // Metrics
        private float _avgFps;
        private float _fpsAccumulator;
        private int _fpsFrames;
        
        private void Start()
        {
            if (_overlayContainer == null)
            {
                // Create container if not assigned
                 GameObject obj = new GameObject("DiagnosticsOverlay", typeof(RectTransform));
                 obj.transform.SetParent(transform, false);
                 _overlayContainer = obj.GetComponent<RectTransform>();
                 _overlayContainer.anchorMin = Vector2.zero;
                 _overlayContainer.anchorMax = Vector2.one;
                 _overlayContainer.offsetMin = Vector2.zero;
                 _overlayContainer.offsetMax = Vector2.zero;
            }

            // Create dot pool
            CreateDotPool();
            
            // Register events
            if (_skeletonController != null)
            {
                _skeletonController.OnPoseDetectionChanged += OnPoseDetectionChanged;
            }
        }

        private void CreateDotPool()
        {
            if (_landmarkDotPrefab == null)
            {
                // Create simpler default dot if no prefab
                GameObject dotObj = new GameObject("Dot", typeof(UIImage));
                dotObj.GetComponent<UIImage>().sprite = null; // White square
                _landmarkDotPrefab = dotObj;
            }

            for (int i = 0; i < 33; i++)
            {
                GameObject dot = Instantiate(_landmarkDotPrefab, _overlayContainer);
                dot.SetActive(false);
                _landmarkDots.Add(dot.GetComponent<UIImage>());
            }
        }

        private void OnPoseDetectionChanged(bool detected)
        {
            if (!detected)
            {
                foreach (var dot in _landmarkDots)
                {
                    dot.gameObject.SetActive(false);
                }
                
                if (_qualityText != null) _qualityText.text = "No Pose Detected";
            }
        }

        private void Update()
        {
            UpdateFPS();

            if (Time.time - _lastUpdate > _updateInterval)
            {
                UpdateDiagnostics();
                _lastUpdate = Time.time;
            }

            if (_showPerLandmarkConfidence && _skeletonController != null)
            {
                UpdateLandmarkOverlay();
            }
        }

        private void UpdateFPS()
        {
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            
            if (_fpsAccumulator > 0.5f)
            {
                _avgFps = _fpsFrames / _fpsAccumulator;
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
            }
        }

        private void UpdateDiagnostics()
        {
            if (_skeletonController == null) return;

            var stats = _skeletonController.GetTrackingStats(0);
            var filterType = _skeletonController.GetCurrentFilterType();
            
            // Build stats string
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"FPS: {_avgFps:F1}");
            sb.AppendLine($"Filter: {filterType}");
            sb.AppendLine($"Visible: {stats.visibleCount}/33");
            sb.AppendLine($"Occluded: {stats.occludedCount}");
            sb.AppendLine($"Confidence: {stats.avgConfidence*100:F0}%");
            
            if (_lowLightEnhancer != null && _lowLightEnhancer.IsEnabled)
            {
                sb.AppendLine($"Brightness: {_lowLightEnhancer.MeasuredBrightness:F2} (x{_lowLightEnhancer.CurrentBrightnessMultiplier:F1})");
            }

            if (_statsText != null)
            {
                _statsText.text = sb.ToString();
            }
            
            // Update quality text
            if (_qualityText != null)
            {
                string quality = "Good";
                UnityColor color = _goodColor;
                
                if (stats.avgConfidence < 0.4f)
                {
                    quality = "Poor Tracking";
                    color = _badColor;
                }
                else if (stats.occludedCount > 5)
                {
                    quality = "Partial Occlusion";
                    color = _warningColor;
                }
                else if (_avgFps < 20)
                {
                    quality = "Low FPS";
                    color = _warningColor;
                }
                
                _qualityText.text = quality;
                _qualityText.color = color;
            }
        }

        private void UpdateLandmarkOverlay()
        {
            // Only support first pose for diagnostics
            for (int i = 0; i < 33; i++)
            {
                var pos = _skeletonController.GetLandmarkPosition(0, i);
                var dot = _landmarkDots[i];
                
                if (pos.HasValue)
                {
                    dot.gameObject.SetActive(true);
                    dot.rectTransform.anchoredPosition = pos.Value;
                    
                    // Get state directly if possible, otherwise infer
                    // Since GetLandmarkPosition handles filtering internally, we need to
                    // check internal state if we exposed it, or infer from skeleton controller
                    
                    // Quick color coding based on inferred state (this is simplified)
                    // In a full implementation, we'd expose per-landmark state from controller
                    dot.color = _goodColor; 
                }
                else
                {
                    dot.gameObject.SetActive(false);
                }
            }
        }

        // Event handler for detailed updates (if hooked up)
        public void OnLandmarkStateUpdated(int poseIndex, int landmarkIndex, LandmarkFilterManager.LandmarkState state)
        {
            if (poseIndex != 0 || landmarkIndex >= _landmarkDots.Count) return;
            
            var dot = _landmarkDots[landmarkIndex];
            
            if (!_showPerLandmarkConfidence)
            {
                if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                return;
            }
            
            UnityColor targetColor = _goodColor;
            
            if (state.IsOccluded)
            {
                targetColor = _occludedColor;
                // Pulse effect for occluded
                float alpha = 0.3f + Mathf.PingPong(Time.time * 2f, 0.4f);
                targetColor.a = alpha;
            }
            else if (state.Confidence < 0.5f)
            {
                targetColor = _warningColor;
            }
            else if (state.Confidence < 0.2f)
            {
                targetColor = _badColor;
            }
            
            dot.color = targetColor;
            
            // Update size based on confidence/status
            float scale = _dotScale * (state.IsOccluded ? 0.7f : 1.0f);
            dot.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
