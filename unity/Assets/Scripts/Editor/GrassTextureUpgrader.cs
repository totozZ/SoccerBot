using UnityEngine;
using UnityEditor;

namespace SoccerBot
{
    public static class GrassTextureUpgrader
    {
        [MenuItem("SoccerBot/Upgrade Grass Texture")]
        public static void Upgrade()
        {
            var grassGO = GameObject.Find("Grass");
            if (grassGO == null)
            { Debug.LogError("[GrassTextureUpgrader] Grass GO not found — enter Play Mode first so FieldBuilder runs"); return; }

            var renderer = grassGO.GetComponent<MeshRenderer>();
            if (renderer == null) { Debug.LogError("[GrassTextureUpgrader] Grass GO missing MeshRenderer"); return; }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Game Buffs/Free Stylized Textures/Textures/Grass_37/Grass_37_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Game Buffs/Free Stylized Textures/Textures/Grass_37/Grass_37_Normal.png");
            if (albedo == null) { Debug.LogError("[GrassTextureUpgrader] Grass_37_Albedo not found"); return; }

            // Duplicate the material so we don't modify the shared default
            var mat = Object.Instantiate(renderer.sharedMaterial);
            mat.name = "GrassMat_Upgraded";

            mat.SetTexture("_BaseMap", albedo);
            if (normal != null) mat.SetTexture("_BumpMap", normal);
            mat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));

            // Save the new material as an asset
            const string savePath = "Assets/Materials/GrassMat_Upgraded.mat";
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, savePath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Upgrade Grass Texture");
            renderer.sharedMaterial = mat;
            EditorUtility.SetDirty(renderer);

            Debug.Log($"[GrassTextureUpgrader] Done — material saved to {savePath}");
        }
    }
}
