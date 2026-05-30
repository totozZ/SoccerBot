using UnityEngine;
using UnityEditor;

namespace SoccerBot
{
    public static class BallMeshReplacer
    {
        [MenuItem("SoccerBot/Replace Ball Mesh with Soccer Prefab")]
        public static void Replace()
        {
            var ballGO = GameObject.Find("Ball");
            if (ballGO == null) { Debug.LogError("[BallMeshReplacer] Ball GO not found"); return; }

            var soccerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Saritasa/Models/Sport_Balls/Soccer.prefab");
            if (soccerPrefab == null) { Debug.LogError("[BallMeshReplacer] Soccer prefab not found"); return; }

            var srcFilter   = soccerPrefab.GetComponentInChildren<MeshFilter>();
            var srcRenderer = soccerPrefab.GetComponentInChildren<MeshRenderer>();
            if (srcFilter == null || srcRenderer == null)
            { Debug.LogError("[BallMeshReplacer] Soccer prefab missing MeshFilter or MeshRenderer"); return; }

            var dstFilter   = ballGO.GetComponent<MeshFilter>();
            var dstRenderer = ballGO.GetComponent<MeshRenderer>();
            if (dstFilter == null || dstRenderer == null)
            { Debug.LogError("[BallMeshReplacer] Ball GO missing MeshFilter or MeshRenderer"); return; }

            Undo.RecordObject(dstFilter,   "Replace Ball Mesh");
            Undo.RecordObject(dstRenderer, "Replace Ball Material");
            Undo.RecordObject(ballGO.transform, "Scale Ball");

            dstFilter.sharedMesh      = srcFilter.sharedMesh;
            dstRenderer.sharedMaterials = srcRenderer.sharedMaterials;
            ballGO.transform.localScale = Vector3.one * 0.22f;

            EditorUtility.SetDirty(ballGO);
            Debug.Log("[BallMeshReplacer] Done — Ball mesh replaced with Soccer prefab mesh.");
        }
    }
}
