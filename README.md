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

### 🌑 Low-Light Enhancement V2
- **Async GPU Processing** — Enhances camera feed without dropping a single frame using `AsyncGPUReadback`.
- **Local Contrast (CLAHE)** — Reveals details in shadows without washing out highlights.
- **Auto-Exposure** — Dynamic brightness adjustment with smooth ring-buffer transitions.

### ⚡ Performance
- **Unity Job System** — Parallelizes filtering for all 33 landmarks across worker threads.
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
│   │   ├── Filtering/          # OneEuro, Kalman, JobSystem
│   │   ├── PoseLandmarkDetection/ # Controllers, Enhancers
│   │   └── Shaders/            # LowLight, Fire Effects
│   └── Editor/                 # Setup Tools
├── Fire Effects/               # VFX Assets
└── StreamingAssets/            # MediaPipe Models
```

---

## 📄 License
MIT License.

---

## 🙏 Acknowledgments
- [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin) by homuler
- [1€ Filter](http://cristal.univ-lille.fr/~casiez/1euro/) by Gery Casiez et al.
