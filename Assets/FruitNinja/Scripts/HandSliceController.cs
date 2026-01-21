using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;

namespace FruitNinja
{
    /// <summary>
    /// Hand tracking controller for slicing fruits.
    /// Extends SimplePoseAnnotationController to receive pose landmark data.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HandSliceController : Mediapipe.Unity.PoseLandmarkSDK.SimplePoseAnnotationController
    {
        [Header("Slice Detection")]
        [SerializeField] private float _minSliceVelocity = 300f; // pixels/second
        [SerializeField] private float _visibilityThreshold = 0.5f;
        [SerializeField] private LayerMask _fruitLayer;
        [SerializeField] private float _sliceRadius = 50f; // pixels
        
        [Header("Trail Effect")]
        [SerializeField] private GameObject _trailPrefab;
        [SerializeField] private Color _trailColor = new Color(1f, 0.5f, 0f, 0.8f);
        
        [Header("Debug")]
        [SerializeField] private bool _showDebug = false;
        
        private RectTransform _rectTransform;
        private Camera _camera;
        
        // Hand tracking data
        private Vector2 _leftHandPos;
        private Vector2 _rightHandPos;
        private Vector2 _prevLeftHandPos;
        private Vector2 _prevRightHandPos;
        private Vector2 _leftHandVelocity;
        private Vector2 _rightHandVelocity;
        private bool _leftHandVisible;
        private bool _rightHandVisible;
        
        // Trail objects
        private GameObject _leftTrail;
        private GameObject _rightTrail;
        private TrailRenderer _leftTrailRenderer;
        private TrailRenderer _rightTrailRenderer;
        
        // Smoothing
        private Vector3 _targetLeftWorldPos;
        private Vector3 _targetRightWorldPos;
        private Vector3 _currentLeftWorldPos;
        private Vector3 _currentRightWorldPos;
        private Vector3 _smoothLeftVel;
        private Vector3 _smoothRightVel;
        [SerializeField] private float _smoothTime = 0.05f; // Interpolation time
        
        // Public accessors for game controller
        public Vector2 LeftHandScreenPos => _leftHandPos;
        public Vector2 RightHandScreenPos => _rightHandPos;
        public Vector2 LeftHandVelocity => _leftHandVelocity;
        public Vector2 RightHandVelocity => _rightHandVelocity;
        public bool IsLeftHandVisible => _leftHandVisible;
        public bool IsRightHandVisible => _rightHandVisible;
        public bool IsAnyHandVisible => _leftHandVisible || _rightHandVisible;
        
        protected override void Start()
        {
            // Create dummy annotation object (required by base class)
            if (annotation != null && annotation.gameObject.scene.name == null)
            {
                var instance = Instantiate(annotation, transform);
                instance.name = "DummySkeleton(Hidden)";
                instance.gameObject.SetActive(false);
                annotation = instance;
            }
            else if (annotation == null)
            {
                var dummyGO = new GameObject("DummySkeleton(Runtime)");
                dummyGO.transform.SetParent(transform, false);
                annotation = dummyGO.AddComponent<MultiPoseLandmarkListAnnotation>();
                dummyGO.SetActive(false);
            }
            
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }
            
            // Force stretch to fill parent
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
            
            _camera = Camera.main;
            
            // Create trails
            CreateTrails();
        }
        
        private void CreateTrails()
        {
            if (_trailPrefab != null)
            {
                _leftTrail = Instantiate(_trailPrefab, transform);
                _leftTrail.name = "LeftHandTrail";
                _leftTrailRenderer = _leftTrail.GetComponent<TrailRenderer>();
                _leftTrail.SetActive(false);
                
                _rightTrail = Instantiate(_trailPrefab, transform);
                _rightTrail.name = "RightHandTrail";
                _rightTrailRenderer = _rightTrail.GetComponent<TrailRenderer>();
                _rightTrail.SetActive(false);
            }
            else
            {
                // Create basic trail if no prefab
                _leftTrail = CreateBasicTrail("LeftHandTrail");
                _leftTrailRenderer = _leftTrail.GetComponent<TrailRenderer>();
                
                _rightTrail = CreateBasicTrail("RightHandTrail");
                _rightTrailRenderer = _rightTrail.GetComponent<TrailRenderer>();
            }
        }
        
        private void Update()
        {
            // Smoothly move trails towards target position in Update loop (runs every frame)
            if (_leftTrail != null && _leftTrail.activeSelf)
            {
                _currentLeftWorldPos = Vector3.SmoothDamp(_currentLeftWorldPos, _targetLeftWorldPos, ref _smoothLeftVel, _smoothTime);
                _leftTrail.transform.position = _currentLeftWorldPos;
            }
            
            if (_rightTrail != null && _rightTrail.activeSelf)
            {
                _currentRightWorldPos = Vector3.SmoothDamp(_currentRightWorldPos, _targetRightWorldPos, ref _smoothRightVel, _smoothTime);
                _rightTrail.transform.position = _currentRightWorldPos;
            }
        }
        
        private GameObject CreateBasicTrail(string name)
        {
            GameObject trailObj = new GameObject(name);
            trailObj.transform.SetParent(transform, false);
            
            TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
            
            // Smoother settings
            trail.time = 0.3f;      // Longer for better flow
            trail.startWidth = 0.25f; // Slight increase for visibility
            trail.endWidth = 0.0f;
            trail.minVertexDistance = 0.005f; // MUCH higher resolution
            trail.numCornerVertices = 20;     // Maximum smoothness
            trail.numCapVertices = 20;        // Maximum roundness
            
            // Create neon material with additive blending for glow
            Material neonMat = new Material(Shader.Find("Sprites/Default"));
            neonMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            neonMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive blending = GLOW
            neonMat.SetInt("_ZWrite", 0);
            neonMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            trail.material = neonMat;
            
            // Blue Neon Gradient (Cyan -> Deep Blue)
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0f, 1f, 1f), 0.0f),    // Cyan (Bright)
                    new GradientColorKey(new Color(0f, 0.4f, 1f), 1.0f)   // Deep Blue
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0.0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1.0f)
                }
            );
            trail.colorGradient = gradient;
            
            // Better width curve
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 1f);
            widthCurve.AddKey(0.2f, 0.8f);
            widthCurve.AddKey(1f, 0f);
            trail.widthCurve = widthCurve;
            
            trail.sortingOrder = 100;
            
            trailObj.SetActive(false);
            return trailObj;
        }
        
        protected override void SyncNow()
        {
            lock (_currentTargetLock)
            {
                isStale = false;
                
                if (_currentTarget.poseLandmarks == null || _currentTarget.poseLandmarks.Count == 0)
                {
                    HideHands();
                    return;
                }
                
                if (_currentTarget.poseLandmarks.Count > 0)
                {
                    var landmarks = _currentTarget.poseLandmarks[0];
                    
                    // Update left hand (wrist index 15, index finger 19)
                    UpdateHand(landmarks.landmarks, 15, 19, ref _leftHandPos, ref _prevLeftHandPos, 
                              ref _leftHandVelocity, ref _leftHandVisible, _leftTrail, _leftTrailRenderer);
                    
                    // Update right hand (wrist index 16, index finger 20)
                    UpdateHand(landmarks.landmarks, 16, 20, ref _rightHandPos, ref _prevRightHandPos,
                              ref _rightHandVelocity, ref _rightHandVisible, _rightTrail, _rightTrailRenderer);
                    
                    // Check for slices
                    if (FruitNinjaGameController.Instance != null && 
                        FruitNinjaGameController.Instance.CurrentState == GameState.Playing)
                    {
                        CheckSlice(_leftHandPos, _leftHandVelocity, _leftHandVisible);
                        CheckSlice(_rightHandPos, _rightHandVelocity, _rightHandVisible);
                    }
                }
                else
                {
                    HideHands();
                }
            }
        }
        
        private void UpdateHand(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks,
                               int wristIndex, int fingerIndex,
                               ref Vector2 handPos, ref Vector2 prevHandPos,
                               ref Vector2 velocity, ref bool isVisible,
                               GameObject trail, TrailRenderer trailRenderer)
        {
            if (wristIndex >= landmarks.Count) return;
            
            var wrist = landmarks[wristIndex];
            
            if (wrist.visibility > _visibilityThreshold || wrist.presence > _visibilityThreshold)
            {
                isVisible = true;
                prevHandPos = handPos;
                
                // Calculate palm position (offset towards finger)
                float palmX = wrist.x;
                float palmY = wrist.y;
                
                if (fingerIndex < landmarks.Count)
                {
                    var finger = landmarks[fingerIndex];
                    float dirX = finger.x - wrist.x;
                    float dirY = finger.y - wrist.y;
                    float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                    
                    if (len > 0.001f)
                    {
                        palmX += (dirX / len) * 0.05f;
                        palmY += (dirY / len) * 0.05f;
                    }
                }
                
                // Convert to screen position
                float w = _rectTransform.rect.width;
                float h = _rectTransform.rect.height;
                
                float screenX = (palmX - 0.5f) * w;
                float screenY = (0.5f - palmY) * h;
                
                handPos = new Vector2(screenX, screenY);
                
                // Calculate velocity (pixels per second)
                velocity = (handPos - prevHandPos) / Time.deltaTime;
                
                // Update trail target (smoothing handles the actual movement in Update)
                if (trail != null)
                {
                    trail.SetActive(true);
                    
                    // Convert to world position for trail target
                    Vector3 worldPos = ScreenToWorldPosition(handPos);
                    
                    if (trail == _leftTrail)
                    {
                        if (_targetLeftWorldPos == Vector3.zero) _currentLeftWorldPos = worldPos; // Snap on first frame
                        _targetLeftWorldPos = worldPos;
                    }
                    else if (trail == _rightTrail)
                    {
                        if (_targetRightWorldPos == Vector3.zero) _currentRightWorldPos = worldPos; // Snap on first frame
                        _targetRightWorldPos = worldPos;
                    }
                    
                    // Show trail only when moving fast
                    if (trailRenderer != null)
                    {
                        trailRenderer.emitting = velocity.magnitude > _minSliceVelocity * 0.5f;
                    }
                }
            }
            else
            {
                isVisible = false;
                velocity = Vector2.zero;
                
                if (trail != null)
                {
                    // trail.SetActive(false); // Don't hide immediately, let trail fade
                    if (trailRenderer != null) trailRenderer.emitting = false;
                }
            }
        }
        
        private void CheckSlice(Vector2 handPos, Vector2 velocity, bool isVisible)
        {
            if (!isVisible) return;
            if (velocity.magnitude < _minSliceVelocity) return;
            
            // Convert hand position to world position
            Vector3 worldPos = ScreenToWorldPosition(handPos);
            
            // Find fruits in range
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, _sliceRadius / 100f);
            
            foreach (var hit in hits)
            {
                Fruit fruit = hit.GetComponent<Fruit>();
                if (fruit != null && !fruit.IsSliced)
                {
                    fruit.TrySlice(velocity, velocity.normalized);
                }
            }
        }
        
        private Vector3 ScreenToWorldPosition(Vector2 screenPos)
        {
            if (_camera == null) _camera = Camera.main;
            
            // Convert from anchored position to screen position
            Vector3 screenPoint = new Vector3(
                Screen.width / 2f + screenPos.x,
                Screen.height / 2f + screenPos.y,
                10f // Camera distance
            );
            
            return _camera.ScreenToWorldPoint(screenPoint);
        }
        
        private void HideHands()
        {
            _leftHandVisible = false;
            _rightHandVisible = false;
            _leftHandVelocity = Vector2.zero;
            _rightHandVelocity = Vector2.zero;
            
            if (_leftTrail != null) _leftTrail.SetActive(false);
            if (_rightTrail != null) _rightTrail.SetActive(false);
        }
        
        private void OnDrawGizmos()
        {
            if (!_showDebug) return;
            
            Gizmos.color = Color.red;
            if (_leftHandVisible)
            {
                Vector3 pos = ScreenToWorldPosition(_leftHandPos);
                Gizmos.DrawWireSphere(pos, _sliceRadius / 100f);
            }
            
            Gizmos.color = Color.blue;
            if (_rightHandVisible)
            {
                Vector3 pos = ScreenToWorldPosition(_rightHandPos);
                Gizmos.DrawWireSphere(pos, _sliceRadius / 100f);
            }
        }
    }
}
