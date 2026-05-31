// StadiumBuilderMenu.cs — Editor menu to drop a Stadium ring around the pitch.
// Menu: SoccerBot → Build Stadium
//
// Creates (or replaces) a "Stadium" GameObject at origin with a fresh procedural
// ring stadium that encircles the existing Field.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerBot.EditorTools
{
    public static class StadiumBuilderMenu
    {
        [MenuItem("SoccerBot/Build Stadium")]
        public static void BuildStadium()
        {
            var existing = GameObject.Find("Stadium");
            if (existing != null) Object.DestroyImmediate(existing);

            var stadium = new GameObject("Stadium");
            var builder = stadium.AddComponent<StadiumBuilder>();
            // Awake builds in play mode; in edit mode we call manually.
            builder.Build();

            Selection.activeGameObject = stadium;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[StadiumBuilder] Stadium built. Save scene to persist.");
        }
    }
}
#endif
