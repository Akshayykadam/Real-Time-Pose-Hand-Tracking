# Pose Landmark Detection SDK

This SDK provides a standalone implementation of MediaPipe's Pose Landmark Detection for Unity.

## Installation

### Local Package
1. Copy the `PoseLandmarkSDK` folder to your Unity project's `Packages` or `Assets` folder.

### Installing Dependencies
This SDK requires the core **MediaPipe Unity Plugin** (`com.github.homuler.mediapipe`).
1. Go to the [MediaPipeUnityPlugin Releases Page](https://github.com/homuler/MediaPipeUnityPlugin/releases).
2. Download the latest `com.github.homuler.mediapipe-*.tgz` file (e.g., `v0.16.2`).
3. Open Unity Package Manager (`Window > Package Manager`).
4. Click the **+** button and select **"Add package from tarball..."**.
5. Select the downloaded `.tgz` file.

## Usage

### 1. Scene Setup
1. Create a `Bootstrap` GameObject in your scene to initialize MediaPipe.
   - You can use the `Bootstrap.cs` script from this SDK (`Mediapipe.Unity.PoseLandmarkSDK.Core.Bootstrap`).
   - Or stick with the default one if you are already using the full plugin.

2. Create a GameObject (e.g., "PoseDetector") and attach the `PoseLandmarkerRunner` script.
   - Namespace: `Mediapipe.Unity.PoseLandmarkSDK`

3. Configure the `PoseLandmarkerRunner` in the Inspector:
   - **Config**: Set model (Heavy/Full/Lite) and running mode (Live Stream / Video / Image).
   - **Image Source**: You need to provide an image source. The SDK includes `WebCamSource`, `StaticImageSource`, etc. in `Mediapipe.Unity.PoseLandmarkSDK.Core`.

### 2. Code Example

```csharp
using UnityEngine;
using Mediapipe.Unity.PoseLandmarkSDK;
using Mediapipe.Unity.PoseLandmarkSDK.Core;

public class MyPoseApp : MonoBehaviour
{
    [SerializeField] private PoseLandmarkerRunner _runner;

    void Start()
    {
        // Ensure Bootstrap is running
        // _runner.Play(); // Called automatically if configured correctly
    }
}
```

## Mobile Support

This SDK is compatible with Android and iOS.

### Permissions
The `WebCamSource` script automatically requests camera permissions on Android and iOS:
- **Android**: Requests `Permission.Camera`.
- **iOS**: Requests `UserAuthorization.WebCam`.

### Configuration
- **Android**: Supports GPU acceleration. The runner automatically checks for OpenGLES3 support to share the GL context.
- **iOS**: Supports CPU/Async read modes.

## Dependencies

- **com.github.homuler.mediapipe**: The core MediaPipe Unity Plugin is required for the underlying native libraries.
