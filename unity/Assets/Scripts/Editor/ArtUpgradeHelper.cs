using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SoccerBot
{
    // One-click art/scene fixer. All scene-object edits use SerializedObject so private
    // [SerializeField] fields get written and persisted. Run "Fix All" then save.
    public static class ArtUpgradeHelper
    {
        const string GrassAlbedoPath = "Assets/Game Buffs/Free Stylized Textures/Textures/Grass_37/Grass_37_Albedo.png";
        const string GrassNormalPath = "Assets/Game Buffs/Free Stylized Textures/Textures/Grass_37/Grass_37_Normal.png";
        const string GrassMatPath    = "Assets/Materials/GrassMat_Upgraded.mat";

        [MenuItem("SoccerBot/Art Upgrade/Fix All")]
        public static void FixAll()
        {
            ReplaceCharacters();
            UpgradeGrassMaterial();
            FixHud();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtUpgrade] Fix All done + scene saved.");
        }

        // ── Characters: human model, no SpongeBob, half height ───────────────
        [MenuItem("SoccerBot/Art Upgrade/Replace Characters")]
        public static void ReplaceCharacters()
        {
            ReplaceCharacter("Teammate", "Assets/DavidJalbert/LowPolyPeople/Prefabs/strong man a.prefab",
                new Color(0.1f, 0.3f, 0.9f));   // blue team
            ReplaceCharacter("Opponent", "Assets/DavidJalbert/LowPolyPeople/Prefabs/normal woman c.prefab",
                new Color(0.9f, 0.1f, 0.1f));   // red team
            Debug.Log("[ArtUpgrade] Characters replaced.");
        }

        private static void ReplaceCharacter(string goName, string prefabPath, Color teamColor)
        {
            var go = FindInScene(goName);
            if (go == null) { Debug.LogWarning($"[ArtUpgrade] {goName} not found"); return; }

            // Turn OFF SpongeBob generation so the procedural primitives never spawn at runtime
            var cb = go.GetComponent<CharacterBuilder>();
            if (cb != null)
            {
                var so = new SerializedObject(cb);
                var prop = so.FindProperty("_buildOnAwake");
                if (prop != null) { prop.boolValue = false; so.ApplyModifiedProperties(); }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning($"[ArtUpgrade] Prefab not found: {prefabPath}"); return; }

            // Replace existing Model child
            var existing = go.transform.Find("Model");
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            model.name = "Model";
            Undo.RegisterCreatedObjectUndo(model, $"Replace {goName} model");

            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = Vector3.one * 0.66f;   // doubled from 0.33 per request

            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", teamColor);
                r.SetPropertyBlock(mpb);
            }
            EditorUtility.SetDirty(go);
        }

        // ── Grass: build textured material as asset, wire to FieldBuilder ────
        // Editor-safe — does not need Play Mode. FieldBuilder applies it every Build().
        [MenuItem("SoccerBot/Art Upgrade/Upgrade Grass Material")]
        public static void UpgradeGrassMaterial()
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassAlbedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassNormalPath);
            if (albedo == null) { Debug.LogError("[ArtUpgrade] Grass_37_Albedo not found"); return; }

            System.IO.Directory.CreateDirectory("Assets/Materials");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GrassMatPath);
            var shader = GetCompatibleLitShader();
            if (shader == null) { Debug.LogError("[ArtUpgrade] No compatible lit shader found"); return; }
            if (mat == null)
            {
                mat = new Material(shader) { name = "GrassMat_Upgraded" };
                AssetDatabase.CreateAsset(mat, GrassMatPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            SetTextureIfPresent(mat, "_BaseMap", albedo);
            SetTextureIfPresent(mat, "_MainTex", albedo);
            if (normal != null)
            {
                SetTextureIfPresent(mat, "_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            SetTextureScaleIfPresent(mat, "_BaseMap", new Vector2(6f, 6f));
            SetTextureScaleIfPresent(mat, "_MainTex", new Vector2(6f, 6f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.22f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.22f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.45f);
            EditorUtility.SetDirty(mat);

            // Wire into FieldBuilder._grassMaterial so the runtime rebuild keeps the texture
            var field = FindInScene("Field");
            var fb = field != null ? field.GetComponent<FieldBuilder>() : Object.FindFirstObjectByType<FieldBuilder>();
            if (fb != null)
            {
                var so = new SerializedObject(fb);
                var prop = so.FindProperty("_grassMaterial");
                if (prop != null) { prop.objectReferenceValue = mat; so.ApplyModifiedProperties(); }
                EditorUtility.SetDirty(fb);
            }
            else Debug.LogWarning("[ArtUpgrade] FieldBuilder not found — material created but not wired");

            Debug.Log("[ArtUpgrade] Grass material upgraded + wired to FieldBuilder.");
        }

        // ── HUD: force _showInDemo on, activate HUD GOs ──────────────────────
        [MenuItem("SoccerBot/Art Upgrade/Fix HUD")]
        public static void FixHud()
        {
            var statusPanel = Object.FindFirstObjectByType<StatusPanel>(FindObjectsInactive.Include);
            if (statusPanel != null)
            {
                var so = new SerializedObject(statusPanel);
                var prop = so.FindProperty("_showInDemo");
                if (prop != null) { prop.boolValue = true; so.ApplyModifiedProperties(); }
                EditorUtility.SetDirty(statusPanel);
            }
            SetActive("HUD",        true);
            SetActive("ScorePanel", true);
            // Note: legacy "PowerBar" GO is a broken duplicate (plain Transform → renders
            // screen-center). Intentionally NOT reactivated. The real bar is "PowerBarUI".
            SetActive("PowerBarUI", true);
            Debug.Log("[ArtUpgrade] HUD fixed (_showInDemo=true + GOs active).");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static GameObject FindInScene(string name)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == name && g.scene.isLoaded) return g;
            return null;
        }

        private static Shader GetCompatibleLitShader()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                && QualitySettings.renderPipeline == null)
            {
                return Shader.Find("Standard") ?? Shader.Find("Diffuse");
            }

            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Diffuse");
        }

        private static void SetTextureIfPresent(Material mat, string propertyName, Texture texture)
        {
            if (mat != null && mat.HasProperty(propertyName))
                mat.SetTexture(propertyName, texture);
        }

        private static void SetTextureScaleIfPresent(Material mat, string propertyName, Vector2 scale)
        {
            if (mat != null && mat.HasProperty(propertyName))
                mat.SetTextureScale(propertyName, scale);
        }

        private static void SetActive(string name, bool active)
        {
            var go = FindInScene(name);
            if (go == null) { Debug.LogWarning($"[ArtUpgrade] {name} not found"); return; }
            Undo.RecordObject(go, $"SetActive {name}");
            go.SetActive(active);
            EditorUtility.SetDirty(go);
        }
    }
}
