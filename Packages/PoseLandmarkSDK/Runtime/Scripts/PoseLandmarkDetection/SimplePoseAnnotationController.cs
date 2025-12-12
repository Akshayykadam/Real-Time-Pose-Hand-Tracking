using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    public class SimplePoseAnnotationController : AnnotationController<MultiPoseLandmarkListAnnotation>
    {
        [SerializeField] private bool _visualizeZ = false;

        protected PoseLandmarkerResult _currentTarget;
        protected readonly object _currentTargetLock = new object();

        public void DrawNow(PoseLandmarkerResult target)
        {
            UpdateCurrentTarget(target);
            SyncNow();
        }

        public void DrawLater(PoseLandmarkerResult target)
        {
            UpdateCurrentTarget(target);
        }

        protected override void Start()
        {
            base.Start();
            if (annotation != null && annotation.gameObject.scene.name == null)
            {
                var instance = Instantiate(annotation, transform);
                instance.name = annotation.name; // Keep name clean
                annotation = instance;
            }

            // FORCE STRETCH COMPONENT
            if (TryGetComponent<RectTransform>(out var rectTransform))
            {
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                // Ensure Z is zeroed for UI overlap
                rectTransform.anchoredPosition3D = new Vector3(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y, 0f);
            }
        }

        protected void UpdateCurrentTarget(PoseLandmarkerResult newTarget)
        {
            lock (_currentTargetLock)
            {
                _currentTarget = newTarget; // Shallow copy usually fine for result struct
                isStale = true;
            }
        }

        protected override void SyncNow()
        {
            lock (_currentTargetLock)
            {
                isStale = false;
                if (_currentTarget.poseLandmarks != null)
                {
                   annotation.Draw(_currentTarget.poseLandmarks, _visualizeZ);
                }
            }
        }
    }
}
