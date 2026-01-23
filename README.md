# 🏃‍♂️ Real-Time Pose & Hand Tracking SDK

A high-performance Unity SDK for real-time human pose and hand landmark detection using **MediaPipe**. This project enables accurate body pose estimation and hand tracking on mobile devices (Android/iOS) and the Unity Editor, featuring industrial-strength filtering and optimization.

![GIF-2025-12-12-18-27-49](https://github.com/user-attachments/assets/83020b12-8e1e-46de-b2ad-4525c188f0d0)


![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)
![MediaPipe](https://img.shields.io/badge/MediaPipe-0.16.2-blue)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS%20%7C%20Editor-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

---

## ✨ Features

### 🧠 Advanced Filtering & Tracking
- **Multi-Algorithm Filtering** — Choose between **One-Euro Filter** (adaptive jitter reduction) or **Kalman Filter** (robust estimation).
- **Occlusion Resilience** — Physics-based trajectory prediction maintains tracking when landmarks are briefly hidden (>1 sec).
- **Adaptive Smoothing** — Automatically smooths output based on movement speed (stable when still, responsive when moving).

### ✋ Hand Tracking
- **21 Landmark Points** — Full hand skeleton with fingertips, knuckles, and palm landmarks.
- **Dual Hand Support** — Track up to 2 hands simultaneously with independent filtering.
- **Finger State Detection** — Built-in `IsFingerExtended()` method for gesture recognition.
- **Gesture Helpers** — Easy access to fingertip positions via `GetFingertipPosition()`.
- **Handedness Detection** — Identify left vs right hand automatically.
- **Same Filtering Pipeline** — Uses the same One-Euro/Kalman filters as body tracking for smooth output.

### 📐 Z-Axis (Depth) Stabilization
- **Axis-Specific Filtering** — Z-axis receives 2x more aggressive smoothing than X/Y to handle inherently noisier depth estimates.
- **Confidence-Weighted Blending** — Low visibility landmarks contribute less to depth calculations, reducing jitter.
- **Anatomical Constraints** — Bone length ratios automatically correct impossible depth values (e.g., arm reaching too far forward).
- **Relative Anchoring** — Z values expressed relative to hip center for improved stability across the skeleton.
- **Multi-Frame Averaging** — Temporal sliding window (configurable 1-10 frames) smooths depth over time.

### 🌑 Low-Light Enhancement V2
- **Async GPU Processing** — Enhances camera feed without dropping a single frame using `AsyncGPUReadback`.
- **Local Contrast (CLAHE)** — Reveals details in shadows without washing out highlights.
- **Auto-Exposure** — Dynamic brightness adjustment with smooth ring-buffer transitions.

### ⚡ Performance
- **Unity Job System** — Parallelizes filtering for all 33 pose + 21×2 hand landmarks across worker threads.
- **Burst Compilation** — Math-heavy operations are optimized to native machine code.
- **Adaptive Frame Skipping** — Automatically adjusts processing rate based on device load (30-60 FPS).
- **Zero-Allocation** — Pre-allocated buffers and object pools minimize GC spikes.

### 📊 Diagnostics
- **Real-Time Analysis Overlay** — Visualizes per-landmark confidence, occlusion status, and tracking quality.
- **Visual Confidence** — Landmarks change color (Green/Yellow/Red) based on visibility.
- **Metric Tracking** — Monitors Jitter, Latency, and Occlusion counts.

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
2. Open in Unity.
3. Install MediaPipe Unity Plugin from [MediaPipe Unity Plugin Releases](https://github.com/homuler/MediaPipeUnityPlugin/releases).

### Scene Setup
Use the automated tool:
1. Go to **Tools > Pose SDK > Setup Scene**
2. Click **Setup Scene** to generate the hierarchy.

---

## ⚙️ Configuration

### Full Body Skeleton Controller
Optimized for different use cases via the `FullBodySkeletonController` component:

| Setting | Description | Recommended |
|---------|-------------|-------------|
| **Filter Type** | `OneEuro` (Jitter removal) vs `Kalman` (Noise estimation) | `OneEuro` |
| **Filter Preset** | `Responsive` (Action), `Balanced` (General), `Smooth` (Yoga/Fitness) | `Balanced` |
| **Occlusion Handling** | Uses trajectory prediction to fill gaps | `True` |
| **Use Job System** | Multithreaded filtering (Critical for mobile) | `True` |

### Hand Skeleton Controller
Configure via the `HandSkeletonController` component:

| Setting | Description | Default |
|---------|-------------|---------|
| **Visibility Threshold** | Minimum confidence to consider landmark visible | 0.3 |
| **Filter Type** | `OneEuro` or `Kalman` (same options as body) | `OneEuro` |
| **Filter Preset** | `Responsive`, `Balanced`, `Smooth`, `VerySmooth` | `Balanced` |
| **Smoothing Factor** | Additional exponential smoothing (0.0-1.0) | 0.5 |
| **Use Job System** | Parallel filtering for both hands | ✓ On |

### Z-Axis (Depth) Stabilization
Found under the **"Z-Axis (Depth) Stabilization"** header in `FullBodySkeletonController`:

| Setting | Description | Default |
|---------|-------------|---------|
| **Enable Z-Axis Stabilization** | Master toggle for all depth improvements | ✓ On |
| **Use Anatomical Constraints** | Corrects impossible Z values using bone lengths | ✓ On |
| **Use Confidence Weighting** | Low visibility = less trust in depth | ✓ On |
| **Z Scale Factor** | Depth variation strength (0.3=flat, 2.0=exaggerated) | 1.0 |
| **Z Sliding Window Size** | Frames to average (higher=smoother, more latency) | 5 |

> **Tip:** For fitness/yoga apps where users move forward/backward, set `Z Scale Factor` to 0.7-0.8 for more stable depth.

### Low Light Enhancer
Located on the **RawImage** object:

| Setting | Description |
|---------|-------------|
| **Enable Local Contrast** | Activates CLAHE-inspired enhancement |
| **Target Brightness** | Desired average scene brightness (0.0 - 1.0) |
| **Noise Reduction** | additional smoothing for grainy sensors |

---

## � Mobile Optimization

### Android
- **Graphics API**: OpenGLES3 (Required for GPU inference)
- **Min API**: Android 7.0 (Nougat)

### iOS
- **Graphics API**: Metal
- **Camera Usage**: Add description in `Info.plist`

### Troubleshooting
- **"IndexOutOfRangeException"**: Ensure `FilteringJobs` uses `[NativeDisableParallelForRestriction]`. (Fixed in v1.1)
- **Black Screen**: Verify `Auto Graphics API` is unchecked and Vulkan is removed.

---

## 📁 Project Structure
```
Assets/
├── PoseLandmarkSDK/
│   ├── Runtime/Scripts/
│   │   ├── Filtering/             # OneEuro, Kalman, ZAxisStabilizer, JobSystem
│   │   ├── PoseLandmarkDetection/ # FullBodySkeletonController, HandSkeletonController
│   │   └── Shaders/               # LowLight, Fire Effects
│   └── Editor/                    # Setup Tools
├── Fire Effects/                  # VFX Assets
└── StreamingAssets/               # MediaPipe Models

Packages/
└── PoseLandmarkSDK/
    └── Runtime/Scripts/
        └── HandLandmarkDetection/ # HandLandmarkerRunner, Configs
```

---

## 📄 License
MIT License.

---

## 🙏 Acknowledgments
- [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin) by homuler
- [1€ Filter](http://cristal.univ-lille.fr/~casiez/1euro/) by Gery Casiez et al.
