using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    [RequireComponent(typeof(RectTransform))]
    public class HandFireballController : SimplePoseAnnotationController
    {
        [Header("Fireball Settings")]
        [SerializeField] private GameObject _fireballPrefab;
        [SerializeField] private float _visibilityThreshold = 0.5f;

        private GameObject _leftHandFireball;
        private GameObject _rightHandFireball;
        private RectTransform _rectTransform;

        protected override void Start()
        {
            // IMPORTANT: We need a VALID 'annotation' object because the Base Class (AnnotationController)
            // accesses 'annotation.isMirrored' and 'annotation.rotationAngle'.
            // If we leave it null, it crashes. 
            // We instantiated a dummy one and HIDE it.
            
            if (annotation != null && annotation.gameObject.scene.name == null)
            {
                var instance = Instantiate(annotation, transform);
                instance.name = "DummySkeleton(Hidden)";
                instance.gameObject.SetActive(false); // Hide the skeleton!
                annotation = instance;
            }
            else if (annotation == null)
            {
                // Create a runtime dummy if none assigned
                var dummyGO = new GameObject("DummySkeleton(Runtime)");
                dummyGO.transform.SetParent(transform, false);
                annotation = dummyGO.AddComponent<MultiPoseLandmarkListAnnotation>();
                dummyGO.SetActive(false);
            }

            // Ensure we have a RectTransform (RequireComponent helps, but safety first)
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }

            // FORCE STRETCH COMPONENT (Copied from Base)
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.anchoredPosition3D = new Vector3(_rectTransform.anchoredPosition.x, _rectTransform.anchoredPosition.y, 0f);

            if (_fireballPrefab != null)
            {
                _leftHandFireball = Instantiate(_fireballPrefab, transform);
                _leftHandFireball.name = "LeftHandFireball";
                EnsureRectTransform(_leftHandFireball);
                _leftHandFireball.SetActive(false);

                _rightHandFireball = Instantiate(_fireballPrefab, transform);
                _rightHandFireball.name = "RightHandFireball";
                EnsureRectTransform(_rightHandFireball);
                _rightHandFireball.SetActive(false);
            }
        }

        private void EnsureRectTransform(GameObject go)
        {
            if (go != null && go.GetComponent<RectTransform>() == null)
            {
                go.AddComponent<RectTransform>();
            }
        }

        protected override void SyncNow()
        {
            lock (_currentTargetLock)
            {
                isStale = false;
                
                if (_currentTarget.poseLandmarks == null || _currentTarget.poseLandmarks.Count == 0)
                {
                    HideFireballs();
                    return;
                }

                // Assuming single pose (index 0) or handling multiple?
                // MultiPoseLandmarkListAnnotation usually iterates.
                // We'll just take the first pose for this specific effect request, or iterate?
                // The structure is PoseLandmarkerResult -> poseLandmarks (List<Landmarks>).
                
                if (_currentTarget.poseLandmarks.Count > 0)
                {
                    var landmarks = _currentTarget.poseLandmarks[0]; // First detected person
                    
                    // Wrist Indices: Left(15), Right(16)
                    UpdateFireball(_leftHandFireball, landmarks.landmarks, 15);
                    UpdateFireball(_rightHandFireball, landmarks.landmarks, 16);
                }
                else
                {
                    HideFireballs();
                }
            }
        }

        private void UpdateFireball(GameObject fireball, List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, int index)
        {
            if (fireball == null) return;
            
            if (index < landmarks.Count)
            {
                var lm = landmarks[index];
                if (lm.visibility > _visibilityThreshold || lm.presence > _visibilityThreshold) // Check visibility
                {
                    fireball.SetActive(true);
                    
                    // Convert coords
                    float w = _rectTransform.rect.width;
                    float h = _rectTransform.rect.height;
                    
                    // MediaPipe: 0,0 Top-Left. Unity UI: 0,0 Center (usually).
                    // x maps 0..1 to -w/2 .. w/2
                    float x = (lm.x - 0.5f) * w;
                    // y maps 0..1 to h/2 .. -h/2 (inverted)
                    float y = (0.5f - lm.y) * h;
                    
                    if (TryGetComponent<RectTransform>(out var rt))
                    {
                         var frt = fireball.GetComponent<RectTransform>();
                         frt.anchoredPosition = new Vector2(x, y);
                         // Force Z to 0 to ensure it's not behind canvas
                         frt.localPosition = new Vector3(frt.localPosition.x, frt.localPosition.y, 0f);
                         // Debug.Log($"Fireball {index} at {x}, {y} (Vis: {lm.visibility})");
                    }
                }
                else
                {
                    // Debug.Log($"Fireball {index} hidden (Low Visibility: {lm.visibility})");
                    fireball.SetActive(false);
                }
            }
        }

        private void HideFireballs()
        {
            if (_leftHandFireball) _leftHandFireball.SetActive(false);
            if (_rightHandFireball) _rightHandFireball.SetActive(false);
        }
    }
}
