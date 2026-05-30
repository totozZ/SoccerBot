using UnityEngine;
using UnityEditor;

namespace SoccerBot
{
    public static class AudioManagerWirer
    {
        [MenuItem("SoccerBot/Wire AudioManager Clips")]
        public static void Wire()
        {
            var mgr = Object.FindFirstObjectByType<AudioManager>();
            if (mgr == null) { Debug.LogError("[AudioManagerWirer] AudioManager not found in scene"); return; }

            var so = new SerializedObject(mgr);

            SetClip(so, "_matchBGM",  "Assets/Gregor Quendel - Free Crowd Cheering Sounds/Gregor Quendel - Free Crowd Cheering Sounds - 09 - Ambience and cheering.wav");
            SetClip(so, "_replayBGM", "Assets/Gregor Quendel - Free Crowd Cheering Sounds/Gregor Quendel - Free Crowd Cheering Sounds - 01 - Strong cheering and strong rhythmic cheering.wav");
            SetClip(so, "_sfxGoal",   "Assets/Gregor Quendel - Free Crowd Cheering Sounds/Gregor Quendel - Free Crowd Cheering Sounds - 03 - Strong cheering - I.wav");
            SetClip(so, "_sfxShoot",  "Assets/Gregor Quendel - Free Crowd Cheering Sounds/Gregor Quendel - Free Crowd Cheering Sounds - 08 - Rhythmic cheering.wav");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mgr);
            Debug.Log("[AudioManagerWirer] Done — clips wired to AudioManager.");
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
