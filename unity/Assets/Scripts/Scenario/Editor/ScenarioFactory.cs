// ScenarioFactory.cs — Editor menu to generate the three default Scenario assets.
// Menu: SoccerBot → Generate Default Scenarios
//
// Field convention: x = sideline lateral, y = height (ground = 0.2 for ball), z = forward.
// Coordinates are tuned for a ~3m × 3m field with the robot near (0,0,-1.5).

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SoccerBot.EditorTools
{
    public static class ScenarioFactory
    {
        private const string ScenariosDir = "Assets/Scenarios";

        [MenuItem("SoccerBot/Generate Default Scenarios")]
        public static void GenerateDefaults()
        {
            EnsureDir(ScenariosDir);

            CreateOrUpdate("ScoreSuccess", BuildScoreSuccess());
            CreateOrUpdate("Intercepted", BuildIntercepted());
            CreateOrUpdate("ShotMissed",  BuildShotMissed());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ScenarioFactory] Generated 3 scenarios in {ScenariosDir}/");
        }

        // ── Defaults ────────────────────────────────────────

        // 球→队友→进球，4s，100 分
        private static Scenario BuildScoreSuccess()
        {
            var s = ScriptableObject.CreateInstance<Scenario>();
            s.scenarioName = "成功传球";
            s.flavorText   = "队友接球后破门得分";
            s.outcome      = ScenarioOutcome.Score;
            s.finalScore   = 100;
            s.duration     = 4f;

            // Ball: robot (0,0.5,-1.5) → mid-air arc → teammate (1.0,0.3,0.5) → goal (1.5,0.3,2.5)
            s.ballPath = new[]
            {
                Wp(0.0f, 0.0f,  0.5f, -1.5f),
                Wp(0.6f, 0.5f,  0.9f, -0.5f),
                Wp(1.2f, 1.0f,  0.5f,  0.5f),  // arrives at teammate
                Wp(2.0f, 1.0f,  0.4f,  0.5f),  // brief possession
                Wp(2.8f, 1.2f,  0.6f,  1.5f),
                Wp(4.0f, 1.5f,  0.3f,  2.5f),  // goal
            };

            // Teammate: starts at (1.0,0,0.5), receives, then drives forward to score
            s.teammatePath = new[]
            {
                WpRot(0.0f, 1.0f, 0f, 0.5f, 0f, 0f, 0f),
                WpRot(1.2f, 1.0f, 0f, 0.5f, 0f, 0f, 0f),
                WpRot(2.0f, 1.0f, 0f, 0.5f, 0f, 30f, 0f),
                WpRot(2.8f, 1.3f, 0f, 1.0f, 0f, 30f, 0f),
                WpRot(4.0f, 1.5f, 0f, 2.0f, 0f, 30f, 0f),
            };

            // Opponent: too far / mistimed, can't intercept
            s.opponentPath = new[]
            {
                WpRot(0.0f, -1.0f, 0f,  1.0f, 0f, 180f, 0f),
                WpRot(2.0f, -0.5f, 0f,  1.2f, 0f, 150f, 0f),
                WpRot(4.0f,  0.5f, 0f,  1.8f, 0f, 100f, 0f),
            };

            return s;
        }

        // 球飞行中被对手抢截，3s，30 分
        private static Scenario BuildIntercepted()
        {
            var s = ScriptableObject.CreateInstance<Scenario>();
            s.scenarioName = "被拦截";
            s.flavorText   = "球被对手抢断，进攻失败";
            s.outcome      = ScenarioOutcome.Intercepted;
            s.finalScore   = 30;
            s.duration     = 3f;

            s.ballPath = new[]
            {
                Wp(0.0f,  0.0f, 0.5f, -1.5f),
                Wp(0.7f,  0.3f, 0.9f, -0.7f),
                Wp(1.4f,  0.4f, 0.6f,  0.0f),  // intercepted here
                Wp(2.2f, -0.3f, 0.3f,  0.3f),  // opponent dribbles back
                Wp(3.0f, -1.0f, 0.3f,  0.0f),
            };

            s.teammatePath = new[]
            {
                WpRot(0.0f, 1.0f, 0f, 0.5f,  0f,  0f, 0f),
                WpRot(1.4f, 1.0f, 0f, 0.5f,  0f,-30f, 0f),  // reacts late
                WpRot(3.0f, 0.8f, 0f, 0.3f,  0f,-90f, 0f),
            };

            s.opponentPath = new[]
            {
                WpRot(0.0f,  0.5f, 0f,  0.5f, 0f, 180f, 0f),
                WpRot(0.7f,  0.4f, 0f,  0.2f, 0f, 200f, 0f),
                WpRot(1.4f,  0.4f, 0f,  0.0f, 0f, 220f, 0f),  // intercepts
                WpRot(2.2f, -0.3f, 0f,  0.3f, 0f, 250f, 0f),
                WpRot(3.0f, -1.0f, 0f,  0.0f, 0f, 270f, 0f),
            };

            return s;
        }

        // 球→队友→射偏，5s，50 分
        private static Scenario BuildShotMissed()
        {
            var s = ScriptableObject.CreateInstance<Scenario>();
            s.scenarioName = "队友射偏";
            s.flavorText   = "队友接球但射门偏出";
            s.outcome      = ScenarioOutcome.Missed;
            s.finalScore   = 50;
            s.duration     = 5f;

            s.ballPath = new[]
            {
                Wp(0.0f, 0.0f, 0.5f, -1.5f),
                Wp(0.6f, 0.4f, 0.9f, -0.5f),
                Wp(1.2f, 0.8f, 0.5f,  0.5f),  // teammate
                Wp(2.5f, 0.8f, 0.4f,  0.5f),  // hold
                Wp(3.4f, 1.4f, 0.7f,  1.6f),
                Wp(4.2f, 2.5f, 0.5f,  2.4f),  // off target — wide right
                Wp(5.0f, 3.2f, 0.2f,  2.8f),
            };

            s.teammatePath = new[]
            {
                WpRot(0.0f, 0.8f, 0f, 0.5f, 0f,  0f, 0f),
                WpRot(1.2f, 0.8f, 0f, 0.5f, 0f,  0f, 0f),
                WpRot(2.5f, 0.8f, 0f, 0.5f, 0f, 60f, 0f),  // aim wrong
                WpRot(3.4f, 1.0f, 0f, 0.8f, 0f, 60f, 0f),
                WpRot(5.0f, 1.5f, 0f, 1.2f, 0f, 60f, 0f),
            };

            s.opponentPath = new[]
            {
                WpRot(0.0f, -0.8f, 0f, 1.0f, 0f, 180f, 0f),
                WpRot(2.5f, -0.4f, 0f, 1.2f, 0f, 200f, 0f),
                WpRot(5.0f,  0.5f, 0f, 1.5f, 0f, 220f, 0f),
            };

            return s;
        }

        // ── Helpers ─────────────────────────────────────────

        private static Waypoint Wp(float t, float x, float y, float z)
            => new Waypoint { t = t, position = new Vector3(x, y, z), eulerRotation = Vector3.zero };

        private static Waypoint WpRot(float t, float x, float y, float z, float rx, float ry, float rz)
            => new Waypoint { t = t, position = new Vector3(x, y, z), eulerRotation = new Vector3(rx, ry, rz) };

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static void CreateOrUpdate(string assetName, Scenario instance)
        {
            string path = $"{ScenariosDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Scenario>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(instance, existing);
                Object.DestroyImmediate(instance);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(instance, path);
            }
        }
    }
}
#endif
