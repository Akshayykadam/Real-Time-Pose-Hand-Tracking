# Pose Landmark Detection SDK - Integration Guide

A complete guide to integrating real-time pose detection into your Unity project.

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| Unity | 2021.3 LTS or higher |
| MediaPipe Unity Plugin | v0.16.0+ |
| Target Platform | Android / iOS / Editor |

---

## Step 1: Install Dependencies

### 1.1 Install MediaPipe Unity Plugin

1. Download the latest `.tgz` from [MediaPipe Unity Plugin Releases](https://github.com/homuler/MediaPipeUnityPlugin/releases)
2. In Unity: **Window → Package Manager → + → Add package from tarball...**
3. Select the downloaded `com.github.homuler.mediapipe-*.tgz` file

### 1.2 Install Pose Landmark SDK

**Option A: Local Package (Recommended)**
1. Copy the `PoseLandmarkSDK` folder to your project's `Packages/` directory
2. Unity will auto-detect and import it

**Option B: From Assets**
1. Copy `PoseLandmarkSDK` folder to `Assets/`

---

## Step 2: Scene Setup (Automated)

The SDK includes an automated setup tool:

1. Go to **Tools → Pose SDK → Setup Scene**
2. This creates a ready-to-use scene with:
   - **Bootstrap** - Initializes MediaPipe
   - **PoseDetector** - Runs AI inference
   - **Canvas/RawImage** - Displays camera feed with pose overlay

> [!TIP]
> The automated setup handles all camera, canvas, and annotation configuration.

---

## Step 3: Manual Scene Setup

If you prefer manual setup:

### 3.1 Create Bootstrap

```
Hierarchy:
└── Bootstrap (GameObject)
    └── Add Component: Bootstrap.cs
```

### 3.2 Create Canvas & Camera Display

```
Hierarchy:
└── Canvas
    ├── Render Mode: Screen Space - Camera
    ├── Render Camera: Main Camera
    └── RawImage
        └── Stretch to fill canvas (Anchor: 0,0 to 1,1)
```

### 3.3 Create Pose Detector

```
Hierarchy:
└── Canvas
    └── RawImage
        └── PoseDetector (GameObject)
            ├── Add Component: PoseLandmarkerRunner
            ├── Add Component: SimplePoseAnnotationController
            └── Annotation: MultiPoseLandmarkListAnnotation (Prefab)
```

### 3.4 Configure Inspector

| Component | Setting | Value |
|-----------|---------|-------|
| PoseLandmarkerRunner | Config → Model | Full (recommended) |
| PoseLandmarkerRunner | Config → Running Mode | Live Stream |
| PoseLandmarkerRunner | Image Source | WebCamSource component |

---

## Step 4: Platform Configuration

### Android (Required)

1. **Project Settings → Player → Android → Other Settings**
2. **Uncheck** `Auto Graphics API`
3. **Remove Vulkan** (not supported by MediaPipe GPU)
4. **Add OpenGLES3** as the primary Graphics API
5. Set **Minimum API Level** to Android 7.0 (API 24)+

### iOS

1. Ensure **Metal** is enabled (default)
2. Add **Camera Usage Description** in Player Settings

---

## Step 5: Optional Features

### 5.1 Hand Fireball Effect

Add visual effects to detected wrist/palm landmarks:

1. Replace `SimplePoseAnnotationController` with `HandFireballController`
2. Assign a fireball VFX prefab to **Fireball Prefab** field
3. Adjust **Palm Offset** to position effect on palm instead of wrist

| Setting | Description |
|---------|-------------|
| Left/Right Palm Offset | Offset from wrist towards palm (normalized coordinates) |
| Use Smart Palm Offset | Auto-calculate direction based on finger landmarks |
| Visibility Threshold | Minimum confidence to show effect (0-1) |

### 5.2 Adjusting Landmark Visuals

1. Locate **MultiPoseLandmarkListAnnotation** prefab
2. Modify **Connection Width** (0-20) for line thickness
3. Modify **Landmark Radius** for point size

### 5.3 Camera Resolution

1. Find **AppSettings** ScriptableObject in project
2. Set **Preferred Default Web Cam Width**:
   - `1920` for Full HD
   - `2560` for 2K
   - `3840` for 4K

---

## Code Examples

### Basic Pose Detection

```csharp
using UnityEngine;
using Mediapipe.Unity.PoseLandmarkSDK;

public class MyPoseApp : MonoBehaviour
{
    [SerializeField] private PoseLandmarkerRunner _runner;

    void Start()
    {
        // Runner auto-starts if configured correctly
        // Access pose data via _runner events
    }
}
```

### Custom Annotation Controller

```csharp
using UnityEngine;
using Mediapipe.Unity.PoseLandmarkSDK;
using Mediapipe.Tasks.Vision.PoseLandmarker;

public class CustomPoseController : SimplePoseAnnotationController
{
    protected override void SyncNow()
    {
        base.SyncNow(); // Draw skeleton
        
        // Access landmarks
        if (_currentTarget.poseLandmarks?.Count > 0)
        {
            var landmarks = _currentTarget.poseLandmarks[0].landmarks;
            // landmarks[0] = nose
            // landmarks[15] = left wrist
            // landmarks[16] = right wrist
            // See MediaPipe documentation for full list
        }
    }
}
```

---

## Landmark Reference

| Index | Landmark |
|-------|----------|
| 0 | Nose |
| 11 | Left Shoulder |
| 12 | Right Shoulder |
| 13 | Left Elbow |
| 14 | Right Elbow |
| 15 | Left Wrist |
| 16 | Right Wrist |
| 19 | Left Index Finger |
| 20 | Right Index Finger |
| 23 | Left Hip |
| 24 | Right Hip |
| 25 | Left Knee |
| 26 | Right Knee |
| 27 | Left Ankle |
| 28 | Right Ankle |

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Black screen on Android** | Ensure OpenGLES3 is set (not Vulkan). Check camera permissions. |
| **Low FPS / Lag** | SDK uses GPU by default. Verify OpenGLES3 is enabled. |
| **Landmarks misaligned** | Ensure PoseDetector is child of RawImage. Canvas must be Screen Space - Camera. |
| **ImageReadMode.GPU not supported** | Normal in Editor. GPU mode only works on device. |
| **Permissions denied** | Uninstall app and reinstall to re-prompt permissions. |

---

## Support

- **MediaPipe Unity Plugin**: [GitHub Repository](https://github.com/homuler/MediaPipeUnityPlugin)
- **Pose Landmark Model**: [MediaPipe Pose Documentation](https://developers.google.com/mediapipe/solutions/vision/pose_landmarker)
