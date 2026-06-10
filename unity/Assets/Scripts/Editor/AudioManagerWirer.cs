using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SoccerBot
{
    public static class AudioManagerWirer
    {
        private const string ClipDir = "Assets/Gregor Quendel - Free Crowd Cheering Sounds/";

        // All clip slots on AudioManager mapped to crowd-cheer wavs. No dedicated SFX
        // exist in the project, so these are approximate crowd sounds — fine for the demo.
        private static readonly (string field, string file)[] Clips =
        {
            ("_introBGM",     "Gregor Quendel - Free Crowd Cheering Sounds - 10 - Ambience.wav"),
            ("_matchBGM",     "Gregor Quendel - Free Crowd Cheering Sounds - 09 - Ambience and cheering.wav"),
            ("_replayBGM",    "Gregor Quendel - Free Crowd Cheering Sounds - 01 - Strong cheering and strong rhythmic cheering.wav"),
            ("_sfxShoot",     "Gregor Quendel - Free Crowd Cheering Sounds - 04 - Strong cheering - II - Short.wav"),
            ("_sfxGoal",      "Gregor Quendel - Free Crowd Cheering Sounds - 03 - Strong cheering - I.wav"),
            ("_sfxIntercept", "Gregor Quendel - Free Crowd Cheering Sounds - 06 - Soft cheering - II.wav"),
            ("_sfxMiss",      "Gregor Quendel - Free Crowd Cheering Sounds - 05 - Soft cheering - I.wav"),
            ("_sfxCrowdCheer","Gregor Quendel - Free Crowd Cheering Sounds - 02 - Strong cheering and soft rhythmic cheering.wav"),
        };

        [MenuItem("SoccerBot/Wire AudioManager Clips")]
        public static void Wire()
        {
            var all = Object.FindObjectsByType<AudioManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Debug.LogError("[AudioManagerWirer] AudioManager not found in scene");
                return;
            }

            // ── Dedup: keep exactly one instance, remove duplicate components ──
            var keeper = PickKeeper(all);
            foreach (var mgr in all)
            {
                if (mgr == keeper) continue;
                Debug.Log($"[AudioManagerWirer] Removing duplicate AudioManager on '{mgr.gameObject.name}'.");
                Undo.DestroyObjectImmediate(mgr);
            }

            // ── Fill all clip slots on the keeper ──
            var so = new SerializedObject(keeper);
            foreach (var (field, file) in Clips)
                SetClip(so, field, ClipDir + file);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(keeper);
            EditorSceneManager.MarkSceneDirty(keeper.gameObject.scene);
            Debug.Log($"[AudioManagerWirer] Done — clips wired to AudioManager on '{keeper.gameObject.name}'. " +
                      "Save the scene (Ctrl+S) to persist.");
        }

        // Prefer the instance on a GameObject named "AudioManager"; otherwise keep the
        // one with the most clips already assigned (avoids keeping an all-empty shell).
        private static AudioManager PickKeeper(AudioManager[] all)
        {
            AudioManager best = null;
            int bestScore = -1;
            foreach (var mgr in all)
            {
                int score = CountAssignedClips(mgr);
                if (mgr.gameObject.name == "AudioManager") score += 100;   // strong preference
                if (score > bestScore) { bestScore = score; best = mgr; }
            }
            return best;
        }

        private static int CountAssignedClips(AudioManager mgr)
        {
            var so = new SerializedObject(mgr);
            int n = 0;
            foreach (var (field, _) in Clips)
            {
                var prop = so.FindProperty(field);
                if (prop != null && prop.objectReferenceValue != null) n++;
            }
            return n;
        }

        private static void SetClip(SerializedObject so, string fieldName, string assetPath)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[AudioManagerWirer] Field not found: {fieldName}"); return; }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) { Debug.LogWarning($"[AudioManagerWirer] Clip not found: {assetPath}"); return; }
            prop.objectReferenceValue = clip;
            Debug.Log($"[AudioManagerWirer] {fieldName} → {clip.name}");
        }
    }
}
