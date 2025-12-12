# Pose Landmark Detection SDK - Integration & Fixes Guide

This document details the setup, configuration, and fixes implemented to achieve a fully functional, high-performance Pose Detection app on Mobile and Editor.

## 1. Quick Start (Setup Scene)

We have created an automated tool to set up the scene instantly.

1.  In the Unity Editor menu, go to **Tools > Pose SDK**.
2.  Click **Setup Scene**.
3.  This will create a [PoseLandmarkDetection](file:///Users/akshaykadam/Documents/UnityApps/Pose%20Landmark%20Detection%20SDK/Packages/PoseLandmarkSDK/Runtime/Scripts/PoseLandmarkDetection/PoseLandmarkDetectionConfig.cs#23-80) scene with:
    *   **Bootstrap**: Initializes MediaPipe.
    *   **PoseDetector**: Runs the AI and handles Rendering.
    *   **Canvas/RawImage**: Displays the camera feed.

## 2. Critical Fixes Implemented

We resolved several blocking issues to ensure stability and visibility:

### 2.1. Rendering & Visibility
*   **Fix**: Visible 3D Landmarks.
    *   *Issue*: Landmarks were invisible because they are 3D meshes, and the default Canvas was "Overlay".
    *   *Solution*: Switched Canvas to **Screen Space - Camera** and linked the Main Camera.
*   **Fix**: Camera Zoom / Scaling.
    *   *Issue*: Camera was zoomed in or cropped on mobile (Portrait vs Landscape mismatch).
    *   *Solution*: Implemented **Rotation-Aware Aspect Fit** in [Screen.cs](file:///Users/akshaykadam/Documents/UnityApps/Pose%20Landmark%20Detection%20SDK/Assets/PoseLandmarkSDK/Runtime/Scripts/Core/Screen.cs). The rendering now automatically handles device rotation and "Letterboxes" the image to ensure the **full field of view** is always visible.

### 2.2. Mobile Specifics
*   **Fix**: Front-Facing Camera Default.
    *   *Solution*: Updated [WebCamSource.cs](file:///Users/akshaykadam/Documents/UnityApps/Pose%20Landmark%20Detection%20SDK/Assets/PoseLandmarkSDK/Runtime/Scripts/Core/ImageSource/WebCamSource.cs) to prioritize the **Front (Selfie)** camera on mobile devices automatically.
*   **Fix**: Android Permissions Crash.
    *   *Issue*: App would fail on first launch because it didn't wait for the user to click "Allow".
    *   *Solution*: Updated permission logic to **WaitUntil** the user explicitly grants permission before initializing the camera.

### 2.3. Performance (Lag on Pixel/High-End Devices)
*   **Fix**: Forced GPU Inference.
    *   *Issue*: The app defaulted to CPU, causing massive lag (2-3 FPS) on devices like Pixel 9 Pro.
    *   *Solution*: Modified [PoseLandmarkDetectionConfig.cs](file:///Users/akshaykadam/Documents/UnityApps/Pose%20Landmark%20Detection%20SDK/Packages/PoseLandmarkSDK/Runtime/Scripts/PoseLandmarkDetection/PoseLandmarkDetectionConfig.cs) to FORCE **ImageReadMode.GPU** on Android/iOS.
    *   *Requirement*: You must use **OpenGLES3** (see Configuration below).

### 2.4. Runtime Errors
*   **Fix**: `NullReferenceException` / Prefab Errors.
    *   *Solution*: Replaced the default mask controller with a custom `SimplePoseAnnotationController` that safely instantiates prefabs at runtime.

---

## 3. Configuration Guide

### 3.1. Setting Resolution (2K / 4K)
To improve detection accuracy/quality:
1.  Find the **[AppSettings](file:///Users/akshaykadam/Documents/UnityApps/Pose%20Landmark%20Detection%20SDK/Packages/PoseLandmarkSDK/Runtime/Scripts/Common/AppSettings.cs#13-93)** asset (ScriptableObject) in your Project.
2.  Under **WebCam Source**, find **Preferred Default Web Cam Width**.
3.  Set it to your desired width (e.g., `2560` for 2K, `3840` for 4K).
4.  The system will attempt to find the closest available resolution.

### 3.2. Adjusting Landmark Visuals
1.  Open the **`MultiPoseLandmarkList Annotation`** prefab (in `Assets/PoseLandmarkSDK/Runtime/Prefabs` or linked in the scene).
2.  Adjust **Connection Width** (Range increased to 0-20) to make lines thicker/thinner.
3.  Adjust **Landmark Radius** to change point size.

---

## 4. Mobile Requirements (CRITICAL)

For the **GPU Performance Mode** to work, you must configure your Player Settings correctly.

### Android
1.  Go to **Project Settings > Player > Android > Other Settings**.
2.  **Uncheck** `Auto Graphics API`.
3.  **Vulkan** is NOT supported by MediaPipe GPU. **Remove it**.
4.  Add/Ensure **OpenGLES3** is the first/only API in the list.
5.  Set **Min API Level** to Android 7.0 (Nougat) or higher.

### iOS
1.  Ensure **Metal** is enabled (standard default).
2.  Camera Usage Description must be set in Info.plist.

---

## 5. Troubleshooting

*   **"ImageReadMode.GPU is not supported" in Editor**:
    *   This is normal. The code automatically falls back to CPU in the Editor to prevent crashes. It only uses GPU on the actual device.
*   **Black Screen on Device**:
    *   Check if you are using **Vulkan**. You must use **OpenGLES3**.
    *   Check if Permissions were denied. Uninstall and Reinstall to prompt again.
*   **Landmarks "Floating" or Misaligned**:
    *   Ensure the **PoseDetector** GameObject is a child of the **RawImage** in the Hierarchy.
    *   Ensure the **Canvas Render Mode** is `Screen Space - Camera`.
