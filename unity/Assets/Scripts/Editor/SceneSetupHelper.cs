using UnityEngine;
using UnityEditor;

namespace SoccerBot
{
    // One-click scene setup: adds AudioManager + IntroManager + ReplayDirector
    // to the scene if they are not already present.
    public static class SceneSetupHelper
    {
        [MenuItem("SoccerBot/Setup Scene Components (AudioManager + Intro + Replay)")]
        public static void SetupComponents()
        {
            // ── AudioManager ──────────────────────────────────
            if (Object.FindFirstObjectByType<AudioManager>() == null)
            {
                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();
                Undo.RegisterCreatedObjectUndo(go, "Add AudioManager");
                Debug.Log("[SceneSetup] Added AudioManager");
            }
            else Debug.Log("[SceneSetup] AudioManager already exists");

            // ── IntroManager + IntroPanel Canvas ─────────────
            if (Object.FindFirstObjectByType<IntroManager>() == null)
            {
                // Canvas
                var canvasGO = new GameObject("IntroCanvas");
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                // Black background image
                var bg = new GameObject("Background");
                bg.transform.SetParent(canvasGO.transform, false);
                var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
                bgImg.color = Color.black;
                var bgRect = bg.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

                // CanvasGroup for fade
                var cg = canvasGO.AddComponent<CanvasGroup>();

                // Three TMP text lines
                string[] lineNames = { "Line1", "Line2", "Line3" };
                float[] yPositions = { 60f, 0f, -60f };
                var tmpLines = new TMPro.TextMeshProUGUI[3];
                for (int i = 0; i < 3; i++)
                {
                    var lineGO = new GameObject(lineNames[i]);
                    lineGO.transform.SetParent(canvasGO.transform, false);
                    var tmp = lineGO.AddComponent<TMPro.TextMeshProUGUI>();
                    tmp.text = "";
                    tmp.fontSize = i == 0 ? 36 : 28;
                    tmp.color = Color.white;
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    var rect = lineGO.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.1f, 0.5f);
                    rect.anchorMax = new Vector2(0.9f, 0.5f);
                    rect.anchoredPosition = new Vector2(0, yPositions[i]);
                    rect.sizeDelta = new Vector2(0, 60);
                    tmpLines[i] = tmp;
                }

                // IntroPanel component
                var introPanel = canvasGO.AddComponent<IntroPanel>();
                // Wire via SerializedObject so private fields get set
                var so = new SerializedObject(introPanel);
                so.FindProperty("_canvasGroup").objectReferenceValue = cg;
                so.FindProperty("_line1").objectReferenceValue = tmpLines[0];
                so.FindProperty("_line2").objectReferenceValue = tmpLines[1];
                so.FindProperty("_line3").objectReferenceValue = tmpLines[2];
                so.ApplyModifiedProperties();

                // IntroManager on MatchFlow GO
                var matchFlowGO = GameObject.Find("MatchFlow");
                if (matchFlowGO == null) matchFlowGO = new GameObject("IntroManager");
                var introMgr = matchFlowGO.AddComponent<IntroManager>();
                var soMgr = new SerializedObject(introMgr);
                soMgr.FindProperty("_introPanel").objectReferenceValue = introPanel;
                soMgr.ApplyModifiedProperties();

                Undo.RegisterCreatedObjectUndo(canvasGO, "Add IntroCanvas");
                Debug.Log("[SceneSetup] Added IntroCanvas + IntroManager");
            }
            else Debug.Log("[SceneSetup] IntroManager already exists");

            // ── ReplayDirector ────────────────────────────────
            if (Object.FindFirstObjectByType<ReplayDirector>() == null)
            {
                var matchFlowGO = GameObject.Find("MatchFlow");
                if (matchFlowGO == null) matchFlowGO = new GameObject("ReplayDirector");
                matchFlowGO.AddComponent<ReplayDirector>();
                Debug.Log("[SceneSetup] Added ReplayDirector to MatchFlow GO");
            }
            else Debug.Log("[SceneSetup] ReplayDirector already exists");

            Debug.Log("[SceneSetup] Scene setup complete. Check AudioManager Inspector to wire audio clips.");
        }
    }
}
