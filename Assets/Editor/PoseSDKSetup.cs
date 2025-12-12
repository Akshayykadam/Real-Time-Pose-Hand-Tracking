using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Reflection;
using System.Linq;

namespace Mediapipe.Unity.PoseLandmarkSDK.Editor
{
    public class PoseSDKSetup : UnityEditor.Editor
    {
        [MenuItem("Pose Setup/Create Scene (Fix)")]
        public static void SetupScene()
        {
            try {
                Debug.Log("Starting Scene Setup (Precise Mode)...");
                
                // 1. Create a new scene
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                
                // 2. Add Main Camera (Crucial for view)
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = UnityEngine.Color.black;
                camObj.transform.position = new Vector3(0, 0, -10);
                Debug.Log("Created Main Camera.");

                // 3. Create Bootstrap
                CreateBootstrap();

                // 4. Create PoseDetector
                CreatePoseDetector();

                // Save Scene
                string scenesDir = "Assets/Scenes";
                if (!System.IO.Directory.Exists(scenesDir))
                {
                    AssetDatabase.CreateFolder("Assets", "Scenes");
                }
                string scenePath = "Assets/Scenes/PoseLandmarkDetection_Fixed.unity";
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"SUCCESS: Scene created at {scenePath}");
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"Setup Failed: {e}");
            }
        }
        
        private static void CreateBootstrap()
        {
             GameObject bootstrapObj = new GameObject("Bootstrap");
             // Search by full name first
             var bootstrapType = FindType("Mediapipe.Unity.PoseLandmarkSDK.Core.Bootstrap");
             if (bootstrapType == null) bootstrapType = FindType("Bootstrap");

             Component bootstrap = null;
             if (bootstrapType != null) {
                bootstrap = bootstrapObj.AddComponent(bootstrapType);
                Debug.Log($"Bootstrap component attached ({bootstrapType.FullName}).");
             } else {
                 Debug.LogError("Could not find Bootstrap type! Checked 'Mediapipe.Unity.PoseLandmarkSDK.Core.Bootstrap'.");
             }
            
            // Handle AppSettings
            Type appSettingsType = FindType("Mediapipe.Unity.PoseLandmarkSDK.Core.AppSettings") ?? FindType("AppSettings");
            
            // Note: I verified manifest earlier, it seemed AppSettings might be in a Sample namespace or missing.
            // If FindType fails, we skip but log it.
            
             if (appSettingsType != null && bootstrap != null)
             {
                 // Try to assign
                 try {
                     var settings = ScriptableObject.CreateInstance(appSettingsType);
                     string settingsPath = "Assets/AppSettings.asset";
                     // Reuse existing
                     string[] guids = AssetDatabase.FindAssets("t:" + appSettingsType.Name);
                     if (guids.Length > 0) {
                         settings = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]), appSettingsType) as ScriptableObject;
                     } else {
                         AssetDatabase.CreateAsset(settings, settingsPath);
                     }
                     
                     SerializedObject so = new SerializedObject(bootstrap);
                     var prop = so.FindProperty("_appSettings");
                     if (prop != null) {
                        prop.objectReferenceValue = settings;
                        so.ApplyModifiedProperties();
                        Debug.Log("Assigned AppSettings.");
                     }
                 } catch (Exception ex) {
                     Debug.LogWarning($"AppSettings issues: {ex.Message}");
                 }
             }
        }

        private static void CreatePoseDetector()
        {
            GameObject detectorObj = new GameObject("PoseDetector");
            
            // Runner
            var runnerType = FindType("Mediapipe.Unity.PoseLandmarkSDK.PoseLandmarkerRunner") ?? FindType("PoseLandmarkerRunner");
            Component runner = null;
            if (runnerType != null) {
                runner = detectorObj.AddComponent(runnerType);
                Debug.Log($"PoseLandmarkerRunner attached ({runnerType.FullName}).");
            } else {
                Debug.LogError("Could not find PoseLandmarkerRunner type!");
            }
            
            // WebCamSource
            var webCamSourceType = FindType("Mediapipe.Unity.PoseLandmarkSDK.Core.WebCamSource") ?? FindType("WebCamSource");
            if (webCamSourceType != null) {
                 detectorObj.AddComponent(webCamSourceType);
                 Debug.Log($"WebCamSource attached ({webCamSourceType.FullName}).");
            } else {
                Debug.LogError("Could not find WebCamSource type!");
            }

            // Annotation Controller
            var annotationControllerType = FindType("Mediapipe.Unity.PoseLandmarkSDK.SimplePoseAnnotationController") ?? FindType("SimplePoseAnnotationController");
            if (annotationControllerType != null && runner != null)
            {
                var annotator = detectorObj.AddComponent(annotationControllerType);
                Debug.Log("SimplePoseAnnotationController attached.");
                
                SerializedObject runnerSO = new SerializedObject(runner);
                var annotatorProp = runnerSO.FindProperty("_poseLandmarkerResultAnnotationController");
                if (annotatorProp != null)
                {
                    annotatorProp.objectReferenceValue = annotator;
                    runnerSO.ApplyModifiedProperties();
                }
            }
        }
        
        private static Type FindType(string typeName)
        {
            // Direct match
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // Scan
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == typeName || type.FullName == typeName)
                        return type;
                }
            }
            return null;
        }
    }
}
