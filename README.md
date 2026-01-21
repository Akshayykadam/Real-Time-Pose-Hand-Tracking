# 🏃‍♂️ Real-Time Pose & Hand Tracking

A high-performance Unity SDK for real-time human pose and hand landmark detection using **MediaPipe**. This project enables accurate body pose estimation and hand tracking on both mobile devices (Android/iOS) and the Unity Editor.

![GIF-2025-12-12-18-27-49](https://github.com/user-attachments/assets/83020b12-8e1e-46de-b2ad-4525c188f0d0)


![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)
![MediaPipe](https://img.shields.io/badge/MediaPipe-0.16.3-blue)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS%20%7C%20Editor-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

---

## ✨ Features

- **Real-Time Pose Detection** — Track 33 body landmarks with high accuracy
- **Hand Tracking** — Detect hand landmarks with custom visual effects (like fire effects!)
- **GPU Acceleration** — Optimized for mobile with GPU inference support
- **Cross-Platform** — Works on Android, iOS, and Unity Editor
- **Easy Setup** — One-click scene setup via Unity Editor tools
- **Customizable Visuals** — Adjustable landmark sizes, connection widths, and custom shaders
- **Automatic Camera Handling** — Front-facing camera default on mobile with rotation-aware aspect fitting

---

## 🚀 Quick Start

### Prerequisites

- **Unity 2021.3** or later
- **MediaPipe Unity Plugin** (`com.github.homuler.mediapipe` v0.16.2+)

### Installation

1. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/Real-Time-Pose---Hand-Tracking.git
   ```

2. Open the project in Unity

3. Install the MediaPipe Unity Plugin:
   - Download from [MediaPipe Unity Plugin Releases](https://github.com/homuler/MediaPipeUnityPlugin/releases)
   - In Unity: `Window > Package Manager > + > Add package from tarball...`
   - Select the downloaded `.tgz` file

### Scene Setup

Use the automated setup tool:

1. Go to **Tools > Pose SDK** in the Unity menu
2. Click **Setup Scene**
3. This creates a complete scene with:
   - **Bootstrap** — Initializes MediaPipe
   - **PoseDetector** — Runs AI inference and handles rendering
   - **Canvas/RawImage** — Displays the camera feed

---

## 📱 Mobile Configuration

### Android Requirements

For GPU acceleration to work properly:

1. Go to **Project Settings > Player > Android > Other Settings**
2. **Uncheck** `Auto Graphics API`
3. **Remove Vulkan** (not supported by MediaPipe GPU)
4. Add **OpenGLES3** as the first/only graphics API
5. Set **Minimum API Level** to Android 7.0 (Nougat) or higher

### iOS Requirements

1. Ensure **Metal** is enabled (default)
2. Add Camera Usage Description in `Info.plist`

---

## ⚙️ Configuration

### Resolution Settings

To adjust detection quality:

1. Find the **AppSettings** ScriptableObject asset
2. Under **WebCam Source**, set **Preferred Default Web Cam Width**:
   - `1920` — Full HD
   - `2560` — 2K
   - `3840` — 4K

### Visual Customization

Modify the **MultiPoseLandmarkList Annotation** prefab:

| Setting | Description | Range |
|---------|-------------|-------|
| Connection Width | Line thickness | 0-20 |
| Landmark Radius | Point size | 0-10 |

---

## 🔥 Special Effects

This project includes custom visual effects:

- **Fire Effects** — Procedural fire shader for hand tracking visualizations
- **Custom Shaders** — Located in `Assets/Fire Effects/`

---

## 📁 Project Structure

```
Real-Time-Pose---Hand-Tracking/
├── Assets/
│   ├── PoseLandmarkSDK/      # Runtime scripts, prefabs, shaders
│   ├── Fire Effects/          # Custom fire effect materials & shaders
│   ├── Scenes/                # Sample scenes
│   └── StreamingAssets/       # MediaPipe model files
├── Packages/
│   └── PoseLandmarkSDK/       # Core SDK package
└── ProjectSettings/           # Unity project settings
```

---

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| **"ImageReadMode.GPU not supported" in Editor** | This is normal — the Editor uses CPU fallback. GPU only works on device. |
| **Black screen on device** | Ensure you're using **OpenGLES3**, not Vulkan. Check camera permissions. |
| **Landmarks floating/misaligned** | Ensure PoseDetector is a child of RawImage. Set Canvas to `Screen Space - Camera`. |
| **Low FPS on high-end devices** | Check that GPU inference is enabled. Verify OpenGLES3 is configured. |
| **Camera permissions crash** | The SDK now waits for explicit user permission before initializing. |

---

## 🛠️ Technical Details

### Key Components

- **PoseLandmarkerRunner** — Main component for pose detection
- **WebCamSource** — Camera input handler with mobile priority
- **SimplePoseAnnotationController** — Safe prefab instantiation for landmarks
- **Screen.cs** — Rotation-aware aspect fitting for proper display

### Performance Optimizations

- Forced GPU inference on Android/iOS for real-time performance
- Automatic fallback to CPU in Unity Editor
- Efficient async image read modes for iOS

---

## 📚 Documentation

For detailed integration instructions, see [Integration_Guide.md](./Integration_Guide.md).

---

## 📄 License

This project is licensed under the MIT License.

---

## 🙏 Acknowledgments

- [MediaPipe](https://mediapipe.dev/) by Google
- [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin) by homuler

---


