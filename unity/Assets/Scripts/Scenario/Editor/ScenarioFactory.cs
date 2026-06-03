// ScenarioFactory.cs — Editor menu to generate the three default Scenario assets.
// Menu: SoccerBot → Generate Default Scenarios
//
// Field convention: x = sideline lateral, y = height, z = forward (toward goal).
// Coordinates are tuned for an 18m × 12m field with the player at world (0,0,2)
// facing -Z, far goal at world z=-9 (~11m in front of the player). The
// scenario player rotates these design-space waypoints by Player.eulerAngles.y
// and translates them to Player.position, so design +Z always means "toward
// the goal the player is looking at."

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

        // 球→队友→进球，6s，100 分
        private static Scenario BuildScoreSuccess()
        {
            var s = ScriptableObject.CreateInstance<Scenario>();
            s.scenarioName = "成功传球";
            s.flavorText   = "队友接球后破门得分";
            s.outcome      = ScenarioOutcome.Score;
            s.finalScore   = 100;
            s.duration     = 6f;

            // Ball: robot pass → teammate (z≈1) → teammate shoots → goal (z≈9)
            s.ballPath = new[]
            {
                Wp(0.0f, 0.0f, 0.5f, -1.5f),  // robot release
                Wp(0.8f, 0.5f, 1.5f, -0.2f),  // pass apex
                Wp(1.6f, 1.0f, 0.5f,  1.0f),  // arrives at teammate
                Wp(2.5f, 1.0f, 0.4f,  1.0f),  // teammate possession
                Wp(3.5f, 1.5f, 1.2f,  3.5f),  // teammate strikes
                Wp(4.5f, 1.0f, 1.5f,  6.0f),  // ball cruising
                Wp(5.5f, 0.3f, 1.0f,  8.0f),  // entering goal mouth
                Wp(6.0f, 0.0f, 0.4f,  9.2f),  // back of net
            };

            s.teammatePath = new[]
            {
                WpRot(0.0f, 1.0f, 0f, 1.0f, 0f,  0f, 0f),
                WpRot(1.6f, 1.0f, 0f, 1.0f, 0f,  0f, 0f),  // receives
                WpRot(2.5f, 1.0f, 0f, 1.0f, 0f,  0f, 0f),  // aim downfield
                WpRot(3.5f, 1.5f, 0f, 2.5f, 0f,  0f, 0f),  // strike position
                WpRot(6.0f, 1.8f, 0f, 4.0f, 0f,  0f, 0f),  // follow-through
            };

            s.opponentPath = new[]
            {
                WpRot(0.0f, -1.5f, 0f, 1.5f, 0f, 180f, 0f),
                WpRot(3.0f, -1.0f, 0f, 3.0f, 0f, 150f, 0f),
                WpRot(6.0f,  0.0f, 0f, 5.0f, 0f, 100f, 0f),  // too late to recover
            };

            return s;
        }

        // 球飞行中被对手抢截，3.5s，30 分
        private static Scenario BuildIntercepted()
        {
            var s = ScriptableObject.CreateInstance<Scenario>();
            s.scenarioName = "被拦截";
            s.flavorText   = "球被对手抢断，进攻失败";
            s.outcome      = ScenarioOutcome.Intercepted;
            s.finalScore   = 30;
            s.duration     = 3.5f;

            s.ballPath = new[]
            {
                Wp(0.0f,  0.0f, 0.5f, -1.5f),
                Wp(0.7f,  0.3f, 1.2f,  0.2f),
                Wp(1.4f,  0.5f, 0.5f,  1.5f),  // intercepted mid-air
                Wp(2.5f, -0.78f, 0.32f,  0.72f),
                Wp(3.5f, -2.35f, 0.32f, -0.5f),
            };

            s.teammatePath = new[]
            {
                WpRot(0.0f, 1.0f, 0f, 1.0f, 0f,   0f, 0f),
                WpRot(1.4f, 1.0f, 0f, 1.0f, 0f, -30f, 0f),  // reacts late
                WpRot(3.5f, 0.5f, 0f, 0.0f, 0f, -90f, 0f),
            };

            s.opponentPath = new[]
            {
                WpRot(0.0f,  0.5f, 0f, 2.0f, 0f, 180f, 0f),
                WpRot(0.7f,  0.5f, 0f, 1.0f, 0f, 200f, 0f),  // closing in
                WpRot(1.4f,  0.5f, 0f, 1.5f, 0f, 220f, 0f),  // intercepts
                WpRot(2.5f, -0.5f, 0f, 0.8f, 0f, 250f, 0f),
                WpRot(3.5f, -2.0f, 0f,-0.5f, 0f, 270f, 0f),
            };

            return s;
        }

        // 球→队友→射偏，6s，50 分
        private static Scenario BuildShotMissed()
        {
            var s = ScriptableObject.CreateInstance<Scenario>();
            s.scenarioName = "队友射偏";
            s.flavorText   = "队友接球但射门偏出";
            s.outcome      = ScenarioOutcome.Missed;
            s.finalScore   = 50;
            s.duration     = 6f;

            s.ballPath = new[]
            {
                Wp(0.0f, 0.0f, 0.5f, -1.5f),
                Wp(0.7f, 0.4f, 1.4f, -0.3f),
                Wp(1.6f, 1.0f, 0.5f,  1.0f),  // teammate
                Wp(2.5f, 1.0f, 0.4f,  1.0f),  // possession
                Wp(3.5f, 1.8f, 1.2f,  3.5f),  // strike (aimed wide)
                Wp(4.5f, 2.8f, 1.6f,  6.0f),
                Wp(5.5f, 3.8f, 1.0f,  8.5f),  // grazes right post outside
                Wp(6.0f, 4.5f, 0.5f,  9.5f),  // out of play
            };

            s.teammatePath = new[]
            {
                WpRot(0.0f, 0.8f, 0f, 1.0f, 0f,  0f, 0f),
                WpRot(1.6f, 0.8f, 0f, 1.0f, 0f,  0f, 0f),
                WpRot(2.5f, 0.8f, 0f, 1.0f, 0f, 30f, 0f),  // bad aim
                WpRot(3.5f, 1.5f, 0f, 2.5f, 0f, 30f, 0f),
                WpRot(6.0f, 2.0f, 0f, 4.0f, 0f, 30f, 0f),
            };

            s.opponentPath = new[]
            {
                WpRot(0.0f, -1.0f, 0f, 1.5f, 0f, 180f, 0f),
                WpRot(3.0f, -0.5f, 0f, 3.0f, 0f, 200f, 0f),
                WpRot(6.0f,  0.5f, 0f, 5.0f, 0f, 220f, 0f),
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
