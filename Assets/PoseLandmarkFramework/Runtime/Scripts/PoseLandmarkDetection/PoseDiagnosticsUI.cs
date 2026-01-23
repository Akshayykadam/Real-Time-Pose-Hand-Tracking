using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Tasks.Components.Containers;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Diagnostics UI overlay showing FPS, pose confidence, and body bounds guidance.
    /// Attach to a Canvas and reference the FullBodySkeletonController.
    /// </summary>
    public class PoseDiagnosticsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FullBodySkeletonController _skeletonController;
        
        [Header("FPS Display")]
        [SerializeField] private bool _showFPS = true;
        [SerializeField] private Text _fpsText;
        
        [Header("Confidence Display")]
        [SerializeField] private bool _showConfidence = true;
        [SerializeField] private Text _confidenceText;
        [SerializeField] private UnityEngine.UI.Image _confidenceBar;
        
        [Header("Body Bounds Guidance")]
        [SerializeField] private bool _showGuidance = true;
        [SerializeField] private RectTransform _guidanceFrame;
        [SerializeField] private Text _guidanceText;
        [SerializeField] private UnityEngine.UI.Image _guidanceFrameImage;
        
        [Header("Guidance Settings")]
        [Tooltip("Target area where user should be (normalized 0-1)")]
        [SerializeField] private UnityEngine.Rect _targetBounds = new UnityEngine.Rect(0.15f, 0.1f, 0.7f, 0.8f);
        
        [Tooltip("Margin tolerance for body position")]
        [SerializeField] private float _marginTolerance = 0.05f;
        
        [Header("Colors")]
        [SerializeField] private UnityEngine.Color _goodColor = new UnityEngine.Color(0.2f, 0.8f, 0.2f, 0.8f);
        [SerializeField] private UnityEngine.Color _warningColor = new UnityEngine.Color(1f, 0.8f, 0f, 0.8f);
        [SerializeField] private UnityEngine.Color _badColor = new UnityEngine.Color(1f, 0.3f, 0.3f, 0.8f);

        // FPS calculation
        private float _deltaTime = 0f;
        private float _fps = 0f;
        private float _fpsUpdateInterval = 0.5f;
        private float _fpsTimer = 0f;
        
        // Pose state
        private float _avgConfidence = 0f;
        private BodyBoundsState _boundsState = BodyBoundsState.NoPose;
        private string _guidanceMessage = "";

        private enum BodyBoundsState
        {
            NoPose,
            TooClose,
            TooFar,
            TooLeft,
            TooRight,
            TooHigh,
            TooLow,
            PartiallyVisible,
            Good
        }

        private void Update()
        {
            UpdateFPS();
            UpdatePoseAnalysis();
            UpdateUI();
        }

        private void UpdateFPS()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _fpsTimer += Time.unscaledDeltaTime;
            
            if (_fpsTimer >= _fpsUpdateInterval)
            {
                _fps = 1.0f / _deltaTime;
                _fpsTimer = 0f;
            }
        }

        private void UpdatePoseAnalysis()
        {
            if (_skeletonController == null)
            {
                _boundsState = BodyBoundsState.NoPose;
                _avgConfidence = 0f;
                _guidanceMessage = "No skeleton controller";
                return;
            }

            int poseCount = _skeletonController.GetDetectedPoseCount();
            
            if (poseCount == 0)
            {
                _boundsState = BodyBoundsState.NoPose;
                _avgConfidence = 0f;
                _guidanceMessage = "Step into frame";
                return;
            }

            // Analyze body position using key landmarks
            AnalyzeBodyBounds();
        }

        private void AnalyzeBodyBounds()
        {
            // Key landmarks for body bounds:
            // 0 = Nose, 11 = Left Shoulder, 12 = Right Shoulder
            // 23 = Left Hip, 24 = Right Hip, 27 = Left Ankle, 28 = Right Ankle
            
            int[] keyLandmarks = { 0, 11, 12, 23, 24, 27, 28, 15, 16 }; // Include wrists
            
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            int visibleCount = 0;
            float totalConfidence = 0f;
            
            foreach (int idx in keyLandmarks)
            {
                Vector3? pos = _skeletonController.GetSmoothedLandmark3D(0, idx);
                if (pos.HasValue)
                {
                    Vector3 p = pos.Value;
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                    visibleCount++;
                    
                    // Estimate confidence from position validity
                    totalConfidence += (p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1) ? 1f : 0.5f;
                }
            }
            
            if (visibleCount < 4)
            {
                _boundsState = BodyBoundsState.PartiallyVisible;
                _avgConfidence = visibleCount / (float)keyLandmarks.Length;
                _guidanceMessage = "Move fully into frame";
                return;
            }
            
            _avgConfidence = totalConfidence / keyLandmarks.Length;
            
            // Calculate body center and size
            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;
            float bodyWidth = maxX - minX;
            float bodyHeight = maxY - minY;
            
            // Check if body fits in target bounds
            float targetCenterX = _targetBounds.x + _targetBounds.width / 2f;
            float targetCenterY = _targetBounds.y + _targetBounds.height / 2f;
            
            // Body too large (too close)
            if (bodyWidth > _targetBounds.width * 1.2f || bodyHeight > _targetBounds.height * 1.1f)
            {
                _boundsState = BodyBoundsState.TooClose;
                _guidanceMessage = "Step back";
                return;
            }
            
            // Body too small (too far)
            if (bodyHeight < _targetBounds.height * 0.4f)
            {
                _boundsState = BodyBoundsState.TooFar;
                _guidanceMessage = "Step closer";
                return;
            }
            
            // Check horizontal position
            if (centerX < _targetBounds.x + _marginTolerance)
            {
                _boundsState = BodyBoundsState.TooLeft;
                _guidanceMessage = "Move right →";
                return;
            }
            if (centerX > _targetBounds.x + _targetBounds.width - _marginTolerance)
            {
                _boundsState = BodyBoundsState.TooRight;
                _guidanceMessage = "← Move left";
                return;
            }
            
            // Check vertical position
            if (minY < _targetBounds.y - _marginTolerance)
            {
                _boundsState = BodyBoundsState.TooHigh;
                _guidanceMessage = "Move down ↓";
                return;
            }
            if (maxY > _targetBounds.y + _targetBounds.height + _marginTolerance)
            {
                _boundsState = BodyBoundsState.TooLow;
                _guidanceMessage = "Move up ↑";
                return;
            }
            
            // All good!
            _boundsState = BodyBoundsState.Good;
            _guidanceMessage = "Perfect!";
            _avgConfidence = Mathf.Min(_avgConfidence * 1.2f, 1f);
        }

        private void UpdateUI()
        {
            // FPS Display
            if (_showFPS && _fpsText != null)
            {
                _fpsText.text = $"FPS: {Mathf.RoundToInt(_fps)}";
                _fpsText.color = _fps >= 30 ? _goodColor : (_fps >= 20 ? _warningColor : _badColor);
            }
            
            // Confidence Display
            if (_showConfidence)
            {
                if (_confidenceText != null)
                {
                    int pct = Mathf.RoundToInt(_avgConfidence * 100f);
                    _confidenceText.text = $"Confidence: {pct}%";
                    _confidenceText.color = GetConfidenceColor(_avgConfidence);
                }
                
                if (_confidenceBar != null)
                {
                    _confidenceBar.fillAmount = _avgConfidence;
                    _confidenceBar.color = GetConfidenceColor(_avgConfidence);
                }
            }
            
            // Guidance Display
            if (_showGuidance)
            {
                if (_guidanceText != null)
                {
                    _guidanceText.text = _guidanceMessage;
                    _guidanceText.color = GetBoundsStateColor();
                }
                
                if (_guidanceFrameImage != null)
                {
                    _guidanceFrameImage.color = GetBoundsStateColor();
                }
            }
        }

        private UnityEngine.Color GetConfidenceColor(float confidence)
        {
            if (confidence >= 0.7f) return _goodColor;
            if (confidence >= 0.4f) return _warningColor;
            return _badColor;
        }

        private UnityEngine.Color GetBoundsStateColor()
        {
            switch (_boundsState)
            {
                case BodyBoundsState.Good:
                    return _goodColor;
                case BodyBoundsState.PartiallyVisible:
                case BodyBoundsState.TooClose:
                case BodyBoundsState.TooFar:
                    return _warningColor;
                default:
                    return _badColor;
            }
        }

        /// <summary>
        /// Check if user is properly positioned in frame
        /// </summary>
        public bool IsUserInPosition()
        {
            return _boundsState == BodyBoundsState.Good;
        }

        /// <summary>
        /// Get current pose confidence (0-1)
        /// </summary>
        public float GetConfidence()
        {
            return _avgConfidence;
        }

        /// <summary>
        /// Get current FPS
        /// </summary>
        public float GetFPS()
        {
            return _fps;
        }

        /// <summary>
        /// Get current guidance message
        /// </summary>
        public string GetGuidanceMessage()
        {
            return _guidanceMessage;
        }
    }
}
