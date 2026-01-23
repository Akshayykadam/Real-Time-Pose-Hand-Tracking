// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.ComponentModel;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.PoseLandmarkSDK.Core;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    public enum HandModelType : int
    {
        [Description("Hand landmarker")]
        HandLandmarker = 0,
    }

    public class HandLandmarkDetectionConfig
    {
        public Tasks.Core.BaseOptions.Delegate Delegate { get; set; } =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            Tasks.Core.BaseOptions.Delegate.CPU;
#else
            Tasks.Core.BaseOptions.Delegate.GPU;
#endif

        public ImageReadMode ImageReadMode { get; set; } =
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            ImageReadMode.GPU;
#else
            ImageReadMode.CPUAsync;
#endif

        public HandModelType Model { get; set; } = HandModelType.HandLandmarker;
        public Tasks.Vision.Core.RunningMode RunningMode { get; set; } = Tasks.Vision.Core.RunningMode.LIVE_STREAM;

        public int NumHands { get; set; } = 2;
        public float MinHandDetectionConfidence { get; set; } = 0.5f;
        public float MinHandPresenceConfidence { get; set; } = 0.5f;
        public float MinTrackingConfidence { get; set; } = 0.5f;

        public string ModelName => Model.GetDescription() ?? Model.ToString();
        public string ModelPath => "hand_landmarker.bytes";

        public HandLandmarkerOptions GetHandLandmarkerOptions(HandLandmarkerOptions.ResultCallback resultCallback = null)
        {
            return new HandLandmarkerOptions(
                new Tasks.Core.BaseOptions(Delegate, modelAssetPath: ModelPath),
                runningMode: RunningMode,
                numHands: NumHands,
                minHandDetectionConfidence: MinHandDetectionConfidence,
                minHandPresenceConfidence: MinHandPresenceConfidence,
                minTrackingConfidence: MinTrackingConfidence,
                resultCallback: resultCallback
            );
        }
    }
}
