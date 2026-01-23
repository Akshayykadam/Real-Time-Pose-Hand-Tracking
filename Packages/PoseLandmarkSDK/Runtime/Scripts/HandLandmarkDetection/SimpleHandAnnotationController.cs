// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Simple annotation controller for hand landmarks.
    /// Handles drawing hand landmark results to MultiHandLandmarkListAnnotation.
    /// </summary>
    public class SimpleHandAnnotationController : AnnotationController<MultiHandLandmarkListAnnotation>
    {
        [SerializeField] private bool _visualizeZ = false;

        protected HandLandmarkerResult _currentTarget;
        protected readonly object _currentTargetLock = new object();

        public void DrawNow(HandLandmarkerResult target)
        {
            UpdateCurrentTarget(target);
            SyncNow();
        }

        public void DrawLater(HandLandmarkerResult target)
        {
            UpdateCurrentTarget(target);
        }

        protected override void Start()
        {
            base.Start();
            if (annotation != null && annotation.gameObject.scene.name == null)
            {
                var instance = Instantiate(annotation, transform);
                instance.name = annotation.name;
                annotation = instance;
            }

            // Force stretch for UI overlay
            if (TryGetComponent<RectTransform>(out var rectTransform))
            {
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                rectTransform.anchoredPosition3D = new Vector3(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y, 0f);
            }
        }

        protected void UpdateCurrentTarget(HandLandmarkerResult newTarget)
        {
            lock (_currentTargetLock)
            {
                _currentTarget = newTarget;
                isStale = true;
            }
        }

        protected override void SyncNow()
        {
            lock (_currentTargetLock)
            {
                isStale = false;
                if (_currentTarget.handLandmarks != null)
                {
                    // Set handedness for proper coloring (left/right hand)
                    annotation.SetHandedness(_currentTarget.handedness);
                    annotation.Draw(_currentTarget.handLandmarks, _visualizeZ);
                }
            }
        }
    }
}
