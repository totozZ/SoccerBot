// FieldBuilderMenu.cs — Editor menu to drop a Field GameObject into the scene.
// Menu: SoccerBot → Build Field
//
// Replaces (or creates) a "Field" GameObject at origin with a fresh procedural
// field. Existing "Ground" plane (if present) gets disabled so it doesn't z-fight.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerBot.EditorTools
{
    public static class FieldBuilderMenu
    {
        [MenuItem("SoccerBot/Build Field")]
        public static void BuildField()
        {
            var existing = GameObject.Find("Field");
            if (existing != null) Object.DestroyImmediate(existing);

            var field = new GameObject("Field");
            var builder = field.AddComponent<FieldBuilder>();
            // Awake builds in play mode; in edit mode we need to call manually.
            builder.Build();

            // Hide legacy single-color Ground plane to avoid z-fighting
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.SetActive(false);
                Debug.Log("[FieldBuilder] Disabled legacy 'Ground' GameObject.");
            }

            Selection.activeGameObject = field;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[FieldBuilder] Field built. Save scene to persist.");
        }
    }
}
#endif
