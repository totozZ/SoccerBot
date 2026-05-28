// PlayerRigBuilder.cs — Editor menu to (re)build the Player rig in the scene.
// Menu: SoccerBot → Build Player Rig
//
// Idempotent: tears down any stale Teammate-mounted FPS rig + root-level
// FpsCamera, then creates a fresh Player GO at (0,0,2) with FpsAnchor/FpsCamera
// children. Also disables XR Camera so it stops stealing render at Play time.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerBot.EditorTools
{
    public static class PlayerRigBuilder
    {
        [MenuItem("SoccerBot/Build Player Rig")]
        public static void BuildPlayerRig()
        {
            // 1) Strip old rig off Teammate
            var teammate = GameObject.Find("Teammate");
            if (teammate != null)
            {
                var fpc = teammate.GetComponent<FPSPlayerController>();
                if (fpc != null) Object.DestroyImmediate(fpc, true);
                var oldAnchor = teammate.transform.Find("FpsAnchor");
                if (oldAnchor != null) Object.DestroyImmediate(oldAnchor.gameObject);
            }

            // 2) Remove root-level FpsCamera leftover from old architecture
            var oldRootCam = GameObject.Find("FpsCamera");
            if (oldRootCam != null && oldRootCam.transform.parent == null)
                Object.DestroyImmediate(oldRootCam);

            // 3) Drop any pre-existing Player so this stays idempotent
            var oldPlayer = GameObject.Find("Player");
            if (oldPlayer != null) Object.DestroyImmediate(oldPlayer);

            // 4) Build fresh Player rig
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, 2f);
            player.transform.eulerAngles = new Vector3(0f, 180f, 0f);
            player.AddComponent<FPSPlayerController>();

            var anchor = new GameObject("FpsAnchor");
            anchor.transform.SetParent(player.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            var camGO = new GameObject("FpsCamera");
            camGO.transform.SetParent(anchor.transform, false);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.depth = 1f;
            camGO.AddComponent<AudioListener>();

            // 5) Stop XR Camera from hijacking render / audio at Play
            var xrCam = GameObject.Find("XR Camera");
            if (xrCam != null)
            {
                var c = xrCam.GetComponent<Camera>();
                if (c != null) c.enabled = false;
                var a = xrCam.GetComponent<AudioListener>();
                if (a != null) a.enabled = false;
            }

            // 6) Mute PC Camera's AudioListener so we don't double up
            var pcCam = GameObject.Find("PC Camera");
            if (pcCam != null)
            {
                var a = pcCam.GetComponent<AudioListener>();
                if (a != null) a.enabled = false;
            }

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PlayerRigBuilder] Player rig built. Save scene (Ctrl+S) to persist.");
        }
    }
}
#endif
