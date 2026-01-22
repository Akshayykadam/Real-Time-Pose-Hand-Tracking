using UnityEngine;
using UnityEditor;
using Mediapipe.Unity.PoseLandmarkSDK;

public class PoseSDKUpgrader : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Pose SDK/Upgrade Scene Components")]
    public static void UpgradeScene()
    {
        // 1. Upgrade Skeleton Controller
        var skeleton = FindObjectOfType<FullBodySkeletonController>();
        if (skeleton != null)
        {
            Debug.Log("Upgrading Skeleton Controller...");
            skeleton.SetOneEuroFilterEnabled(true);
            skeleton.SetOcclusionHandling(true);
            skeleton.SetAdaptiveSmoothing(true);
            EditorUtility.SetDirty(skeleton);
        }
        else
        {
            Debug.LogWarning("Detailed FullBodySkeletonController not found in scene.");
        }

        // 2. Upgrade Low Light Enhancer
        var enhancer = FindObjectOfType<LowLightEnhancer>();
        if (enhancer == null)
        {
            var rawImage = FindObjectOfType<UnityEngine.UI.RawImage>();
            if (rawImage != null)
            {
                Debug.Log("Adding LowLightEnhancer to RawImage...");
                enhancer = rawImage.gameObject.AddComponent<LowLightEnhancer>();
            }
        }
        
        if (enhancer != null)
        {
            Debug.Log("Configuring LowLightEnhancer...");
            enhancer.EnableAutoAdjust(true);
            enhancer.SetAdvancedSettings(0.3f, 0.2f, 0.1f);
            EditorUtility.SetDirty(enhancer);
        }

        // 3. Add Advanced Diagnostics
        var diagnostics = FindObjectOfType<AdvancedPoseDiagnostics>();
        if (diagnostics == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                Debug.Log("Adding AdvancedPoseDiagnostics...");
                GameObject diagObj = new GameObject("PoseDiagnostics");
                diagObj.transform.SetParent(canvas.transform, false);
                diagnostics = diagObj.AddComponent<AdvancedPoseDiagnostics>();
                
                // Try to link references
                SerializedObject so = new SerializedObject(diagnostics);
                so.FindProperty("_skeletonController").objectReferenceValue = skeleton;
                so.FindProperty("_lowLightEnhancer").objectReferenceValue = enhancer;
                so.ApplyModifiedProperties();
            }
        }
        
        Debug.Log("Pose SDK Upgrade Complete! Don't forget to assign UI references if needed.");
    }
#endif
}
