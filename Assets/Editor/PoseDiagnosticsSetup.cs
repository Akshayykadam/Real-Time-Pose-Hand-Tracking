using UnityEngine;
using UnityEditor;

namespace Mediapipe.Unity.PoseLandmarkSDK.Editor
{
    public class PoseDiagnosticsSetup : UnityEditor.Editor
    {
        [MenuItem("Pose Setup/Add Diagnostics UI")]
        public static void AddDiagnosticsUI()
        {
            // Find the canvas in the scene
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found in scene. Please add a Canvas first.");
                return;
            }

            // Find skeleton controller
            FullBodySkeletonController skeletonController = Object.FindObjectOfType<FullBodySkeletonController>();

            // ==================== DIAGNOSTICS PANEL (Top-Left) ====================
            GameObject panel = new GameObject("DiagnosticsPanel");
            panel.transform.SetParent(canvas.transform, false);
            
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(15, -15);
            panelRect.sizeDelta = new Vector2(220, 130);

            // Panel background with rounded feel
            UnityEngine.UI.Image panelBg = panel.AddComponent<UnityEngine.UI.Image>();
            panelBg.color = new UnityEngine.Color(0.08f, 0.08f, 0.12f, 0.85f);

            // Add layout
            UnityEngine.UI.VerticalLayoutGroup layout = panel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;

            // ---------- FPS Row ----------
            GameObject fpsRow = CreateRow(panel.transform, "FPS Row");
            GameObject fpsLabel = CreateLabel(fpsRow.transform, "FPS", 12, TextAnchor.MiddleLeft, new UnityEngine.Color(0.6f, 0.6f, 0.7f));
            GameObject fpsValue = CreateLabel(fpsRow.transform, "--", 16, TextAnchor.MiddleRight, UnityEngine.Color.white);
            UnityEngine.UI.Text fpsText = fpsValue.GetComponent<UnityEngine.UI.Text>();
            fpsText.fontStyle = FontStyle.Bold;

            // ---------- Confidence Row ----------
            GameObject confRow = CreateRow(panel.transform, "Confidence Row");
            GameObject confLabel = CreateLabel(confRow.transform, "Confidence", 12, TextAnchor.MiddleLeft, new UnityEngine.Color(0.6f, 0.6f, 0.7f));
            GameObject confValue = CreateLabel(confRow.transform, "--%", 16, TextAnchor.MiddleRight, UnityEngine.Color.white);
            UnityEngine.UI.Text confText = confValue.GetComponent<UnityEngine.UI.Text>();
            confText.fontStyle = FontStyle.Bold;

            // ---------- Confidence Bar ----------
            GameObject barContainer = new GameObject("ConfidenceBarContainer");
            barContainer.transform.SetParent(panel.transform, false);
            RectTransform barContRect = barContainer.AddComponent<RectTransform>();
            barContRect.sizeDelta = new Vector2(196, 8);
            
            // Bar background
            UnityEngine.UI.Image barBg = barContainer.AddComponent<UnityEngine.UI.Image>();
            barBg.color = new UnityEngine.Color(0.15f, 0.15f, 0.2f, 1f);
            
            // Bar fill
            GameObject barFill = new GameObject("ConfidenceBarFill");
            barFill.transform.SetParent(barContainer.transform, false);
            RectTransform barFillRect = barFill.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = new Vector2(0.5f, 1f);
            barFillRect.offsetMin = new Vector2(2, 2);
            barFillRect.offsetMax = new Vector2(-2, -2);
            barFillRect.pivot = new Vector2(0, 0.5f);
            
            UnityEngine.UI.Image barFillImg = barFill.AddComponent<UnityEngine.UI.Image>();
            barFillImg.color = new UnityEngine.Color(0.3f, 0.85f, 0.4f, 1f);

            // ---------- Spacer ----------
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(panel.transform, false);
            RectTransform spacerRect = spacer.AddComponent<RectTransform>();
            spacerRect.sizeDelta = new Vector2(0, 4);

            // ---------- Status Row ----------
            GameObject statusRow = new GameObject("StatusRow");
            statusRow.transform.SetParent(panel.transform, false);
            RectTransform statusRect = statusRow.AddComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(196, 24);
            
            // Status indicator dot
            GameObject statusDot = new GameObject("StatusDot");
            statusDot.transform.SetParent(statusRow.transform, false);
            RectTransform dotRect = statusDot.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0, 0.5f);
            dotRect.anchorMax = new Vector2(0, 0.5f);
            dotRect.pivot = new Vector2(0, 0.5f);
            dotRect.anchoredPosition = new Vector2(0, 0);
            dotRect.sizeDelta = new Vector2(10, 10);
            
            UnityEngine.UI.Image dotImg = statusDot.AddComponent<UnityEngine.UI.Image>();
            dotImg.color = new UnityEngine.Color(0.3f, 0.85f, 0.4f, 1f);

            // Status text
            GameObject statusTextObj = CreateLabel(statusRow.transform, "Initializing...", 13, TextAnchor.MiddleLeft, UnityEngine.Color.white);
            RectTransform statusTextRect = statusTextObj.GetComponent<RectTransform>();
            statusTextRect.anchorMin = new Vector2(0, 0);
            statusTextRect.anchorMax = new Vector2(1, 1);
            statusTextRect.offsetMin = new Vector2(16, 0);
            statusTextRect.offsetMax = Vector2.zero;
            UnityEngine.UI.Text guidanceText = statusTextObj.GetComponent<UnityEngine.UI.Text>();

            // ==================== GUIDANCE FRAME (Center) ====================
            GameObject frameObj = new GameObject("GuidanceFrame");
            frameObj.transform.SetParent(canvas.transform, false);
            RectTransform frameRect = frameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.1f, 0.05f);
            frameRect.anchorMax = new Vector2(0.9f, 0.95f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            // Frame corners
            CreateFrameCorner(frameObj.transform, new Vector2(0, 1), new Vector2(0, 1), "TopLeft");
            CreateFrameCorner(frameObj.transform, new Vector2(1, 1), new Vector2(1, 1), "TopRight");
            CreateFrameCorner(frameObj.transform, new Vector2(0, 0), new Vector2(0, 0), "BottomLeft");
            CreateFrameCorner(frameObj.transform, new Vector2(1, 0), new Vector2(1, 0), "BottomRight");

            // Frame indicator image (for color changes)
            UnityEngine.UI.Image frameImg = frameObj.AddComponent<UnityEngine.UI.Image>();
            frameImg.color = new UnityEngine.Color(1f, 1f, 1f, 0f); // Invisible, just for reference

            // ==================== ADD DIAGNOSTICS COMPONENT ====================
            PoseDiagnosticsUI diagnostics = panel.AddComponent<PoseDiagnosticsUI>();
            
            SerializedObject so = new SerializedObject(diagnostics);
            so.FindProperty("_skeletonController").objectReferenceValue = skeletonController;
            so.FindProperty("_fpsText").objectReferenceValue = fpsText;
            so.FindProperty("_confidenceText").objectReferenceValue = confText;
            so.FindProperty("_confidenceBar").objectReferenceValue = barFillImg;
            so.FindProperty("_guidanceText").objectReferenceValue = guidanceText;
            so.FindProperty("_guidanceFrame").objectReferenceValue = frameRect;
            so.FindProperty("_guidanceFrameImage").objectReferenceValue = dotImg; // Use dot for color
            so.ApplyModifiedProperties();

            Selection.activeGameObject = panel;
            Debug.Log("✓ Diagnostics UI created! Assign SkeletonController if not auto-detected.");
        }

        private static GameObject CreateRow(Transform parent, string name)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(parent, false);
            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(196, 22);
            
            UnityEngine.UI.HorizontalLayoutGroup hlg = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            
            return row;
        }

        private static GameObject CreateLabel(Transform parent, string text, int fontSize, TextAnchor align, UnityEngine.Color color)
        {
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(parent, false);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 22);
            
            UnityEngine.UI.Text txt = textObj.AddComponent<UnityEngine.UI.Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = align;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null)
            {
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            
            return textObj;
        }

        private static void CreateFrameCorner(Transform parent, Vector2 anchorMin, Vector2 anchorMax, string name)
        {
            GameObject corner = new GameObject($"Corner_{name}");
            corner.transform.SetParent(parent, false);
            
            RectTransform rect = corner.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMax;
            
            // Adjust position based on corner
            float size = 40f;
            float thickness = 3f;
            
            // Horizontal line
            GameObject hLine = new GameObject("HLine");
            hLine.transform.SetParent(corner.transform, false);
            RectTransform hRect = hLine.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(anchorMin.x, anchorMax.y > 0.5f ? 1 : 0);
            hRect.anchorMax = new Vector2(anchorMin.x, anchorMax.y > 0.5f ? 1 : 0);
            hRect.pivot = new Vector2(anchorMax.x, anchorMax.y > 0.5f ? 1 : 0);
            hRect.sizeDelta = new Vector2(size, thickness);
            
            UnityEngine.UI.Image hImg = hLine.AddComponent<UnityEngine.UI.Image>();
            hImg.color = new UnityEngine.Color(1f, 1f, 1f, 0.5f);
            
            // Vertical line
            GameObject vLine = new GameObject("VLine");
            vLine.transform.SetParent(corner.transform, false);
            RectTransform vRect = vLine.AddComponent<RectTransform>();
            vRect.anchorMin = new Vector2(anchorMax.x > 0.5f ? 1 : 0, anchorMin.y);
            vRect.anchorMax = new Vector2(anchorMax.x > 0.5f ? 1 : 0, anchorMin.y);
            vRect.pivot = new Vector2(anchorMax.x > 0.5f ? 1 : 0, anchorMax.y);
            vRect.sizeDelta = new Vector2(thickness, size);
            
            UnityEngine.UI.Image vImg = vLine.AddComponent<UnityEngine.UI.Image>();
            vImg.color = new UnityEngine.Color(1f, 1f, 1f, 0.5f);
        }
    }
}
