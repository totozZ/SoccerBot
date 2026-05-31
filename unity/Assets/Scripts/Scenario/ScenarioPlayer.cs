// ScenarioPlayer.cs — Plays a Scenario by linearly interpolating waypoints
// for the ball, teammate, and opponent transforms. Slow-mo on the last second.
//
// Lifecycle:
//   Play(scenario)
//     → BallController.BeginExternalControl()  (suppresses GameManager-driven ball pose)
//     → activate teammate/opponent renderers
//     → coroutine drives transforms each frame using SampleAt()
//     → last second: Time.timeScale = slowMoScale (uses unscaledDeltaTime internally)
//     → restore timeScale, EndExternalControl(), fire OnScenarioComplete

using System;
using System.Collections;
using UnityEngine;

namespace SoccerBot
{
    public class ScenarioPlayer : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform _ballTransform;
        [SerializeField] private Transform _teammateTransform;
        [SerializeField] private Transform _opponentTransform;
        [SerializeField] private Transform _robotTransform;      // P5.1: for ball-origin offset
        [SerializeField] private BallController _ballController;

        [Header("Slow Motion")]
        [SerializeField] private bool _slowMoLastSecond = true;
        [SerializeField, Range(0.05f, 1f)] private float _slowMoScale = 0.5f;

        [Header("NPC Spawn Blend")]
        [Tooltip("NPCs blend from their Setup position to path[0] over this duration (seconds). Set 0 to disable.")]
        [SerializeField, Range(0f, 1f)] private float _npcSpawnBlendDuration = 0.4f;

        [Header("Intercept Pulse")]
        [Tooltip("Opponent does a quick scale pulse when the Intercepted scenario plays.")]
        [SerializeField] private bool _interceptPulseEnabled = true;
        [SerializeField, Range(0f, 2f)] private float _interceptPulseDelay = 0.5f;   // seconds into scenario before pulse

        public event Action<Scenario> OnScenarioComplete;

        public bool IsPlaying { get; private set; }

        // Expose NPC refs so MatchFlowController can reposition them during Setup
        // (GameObject.Find skips inactive root GOs, so it can't grab Opponent itself).
        public Transform TeammateTransform => _teammateTransform;
        public Transform OpponentTransform => _opponentTransform;

        // P7.2 fix: let MatchFlowController swap the origin from Robot to Teammate
        // before playing a scenario, so the ball starts at the player's feet and
        // the rotation aligns with the goal direction (Teammate is pre-rotated to face the goal).
        public void SetOrigin(Transform t)
        {
            _robotTransform = t;
        }

        public void Play(Scenario scenario)
        {
            if (scenario == null)
            {
                Debug.LogWarning("[ScenarioPlayer] Play called with null scenario.");
                return;
            }
            if (IsPlaying)
            {
                Debug.LogWarning($"[ScenarioPlayer] Already playing; ignored '{scenario.scenarioName}'.");
                return;
            }
            StartCoroutine(PlayCoroutine(scenario));
        }

        private IEnumerator PlayCoroutine(Scenario s)
        {
            IsPlaying = true;
            Debug.Log($"[ScenarioPlayer] Playing: {s.scenarioName} ({s.outcome}, {s.finalScore})");

            if (_ballController != null) _ballController.BeginExternalControl();

            // Kick off intercept pulse on a separate coroutine so it doesn't block playback.
            if (_interceptPulseEnabled && s.outcome == ScenarioOutcome.Intercepted && _opponentTransform != null)
                StartCoroutine(InterceptPulse(_opponentTransform, _interceptPulseDelay));

            // P5.1: Offset all waypoints so the ball originates from the robot's
            // current world position instead of the design-time (0, 0.5, -1.5).
            // P7.2 fix: Y offset must be zero — the ball keyframes already encode
            // ground-relative heights (0.3-0.9). Subtracting designOrigin.y (0.5)
            // would push the ball below ground (e.g. last keyframe Y=0.3 → -0.2).
            Vector3 posOffset = Vector3.zero;
            Quaternion rotOffset = Quaternion.identity;
            Vector3 designOrigin = Vector3.zero;
            if (_robotTransform != null && s.ballPath != null && s.ballPath.Length > 0)
            {
                designOrigin = s.ballPath[0].position;
                Vector3 origin = _robotTransform.position;
                posOffset = new Vector3(origin.x - designOrigin.x, 0f, origin.z - designOrigin.z);
                rotOffset = Quaternion.Euler(0f, _robotTransform.eulerAngles.y, 0f);
            }

            // Ball gets the full offset. Teammate/Opponent keep their original Y
            // so they stay on the ground instead of sinking when designOrigin.y != 0.
            var ballPath     = OffsetPath(s.ballPath,     posOffset, rotOffset, designOrigin, false);
            var teammatePath = OffsetPath(s.teammatePath, posOffset, rotOffset, designOrigin, true);
            var opponentPath = OffsetPath(s.opponentPath, posOffset, rotOffset, designOrigin, true);

            float elapsed = 0f;
            float slowMoBoundary = Mathf.Max(0f, s.duration - 1f);
            bool slowMoApplied = false;

            // Record NPC positions set by MatchFlowController during Setup,
            // so we can blend from there to path[0] instead of teleporting.
            Vector3 teammateSpawnPos = _teammateTransform != null ? _teammateTransform.position : Vector3.zero;
            Quaternion teammateSpawnRot = _teammateTransform != null ? _teammateTransform.rotation : Quaternion.identity;
            Vector3 opponentSpawnPos = _opponentTransform != null ? _opponentTransform.position : Vector3.zero;
            Quaternion opponentSpawnRot = _opponentTransform != null ? _opponentTransform.rotation : Quaternion.identity;

            // Snap ball to t=0, reveal NPCs at their Setup positions.
            // NPCs blend to path[0] over _npcSpawnBlendDuration rather than teleporting.
            if (_ballTransform != null) SampleTo(_ballTransform, ballPath, 0f);
            SetActive(_teammateTransform, true);
            SetActive(_opponentTransform, true);

            while (elapsed < s.duration)
            {
                if (_slowMoLastSecond && !slowMoApplied && elapsed >= slowMoBoundary)
                {
                    // Smooth transition into slow-mo instead of snapping
                    slowMoApplied = true;
                }

                if (slowMoApplied)
                {
                    Time.timeScale = Mathf.Lerp(Time.timeScale, _slowMoScale, Time.unscaledDeltaTime * 6f);
                    if (Time.timeScale < _slowMoScale + 0.01f) Time.timeScale = _slowMoScale;
                }

                ApplySample(ballPath, teammatePath, opponentPath, elapsed);

                // During the spawn blend window, override NPC positions with a lerp
                // from their Setup position to path[0], masking the teleport.
                if (_npcSpawnBlendDuration > 0f && elapsed < _npcSpawnBlendDuration)
                {
                    float blendT = elapsed / _npcSpawnBlendDuration;
                    if (teammatePath != null && teammatePath.Length > 0 && _teammateTransform != null)
                    {
                        _teammateTransform.position = Vector3.Lerp(teammateSpawnPos, teammatePath[0].position, blendT);
                        _teammateTransform.rotation = Quaternion.Slerp(teammateSpawnRot, teammatePath[0].Rotation, blendT);
                    }
                    if (opponentPath != null && opponentPath.Length > 0 && _opponentTransform != null)
                    {
                        _opponentTransform.position = Vector3.Lerp(opponentSpawnPos, opponentPath[0].position, blendT);
                        _opponentTransform.rotation = Quaternion.Slerp(opponentSpawnRot, opponentPath[0].Rotation, blendT);
                    }
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            // Final frame at t = duration.
            ApplySample(ballPath, teammatePath, opponentPath, s.duration);

            if (slowMoApplied)
            {
                // Smooth recovery back to normal speed
                float t = 0f;
                while (t < 0.3f)
                {
                    t += Time.unscaledDeltaTime;
                    Time.timeScale = Mathf.Lerp(_slowMoScale, 1f, t / 0.3f);
                    yield return null;
                }
                Time.timeScale = 1f;
            }
            if (_ballController != null) _ballController.EndExternalControl();

            // Clear the stage — NPCs disappear after the scenario ends so the
            // pre-scenario tableau is just the robot standing alone.
            SetActive(_teammateTransform, false);
            SetActive(_opponentTransform, false);

            IsPlaying = false;
            OnScenarioComplete?.Invoke(s);
        }

        // P5.1: overload accepting pre-offset local path copies.
        private void ApplySample(Waypoint[] ballPath, Waypoint[] teammatePath, Waypoint[] opponentPath, float t)
        {
            if (_ballTransform != null)     SampleTo(_ballTransform,     ballPath,     t);
            if (_teammateTransform != null) SampleTo(_teammateTransform, teammatePath, t);
            if (_opponentTransform != null) SampleTo(_opponentTransform, opponentPath, t);
        }

        // P5.1: translate + yaw-rotate a waypoint array around the design origin.
        // When keepY is true, the original Y is preserved (for ground-bound NPCs).
        private static Waypoint[] OffsetPath(Waypoint[] path, Vector3 offset, Quaternion rotation, Vector3 designOrigin, bool keepY)
        {
            if (path == null) return null;
            var result = new Waypoint[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                result[i] = path[i];
                var local = path[i].position - designOrigin;
                var newPos = rotation * local + designOrigin + offset;
                if (keepY) newPos.y = path[i].position.y;
                result[i].position = newPos;
                result[i].eulerRotation = (rotation * Quaternion.Euler(path[i].eulerRotation)).eulerAngles;
            }
            return result;
        }

        private static void SampleTo(Transform target, Waypoint[] path, float t)
        {
            if (path == null || path.Length == 0) return;

            if (path.Length == 1 || t <= path[0].t)
            {
                target.position = path[0].position;
                target.rotation = path[0].Rotation;
                return;
            }

            var last = path[path.Length - 1];
            if (t >= last.t)
            {
                target.position = last.position;
                target.rotation = last.Rotation;
                return;
            }

            for (int i = 0; i < path.Length - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                if (t >= a.t && t <= b.t)
                {
                    float span = Mathf.Max(0.0001f, b.t - a.t);
                    float u = (t - a.t) / span;
                    target.position = Vector3.Lerp(a.position, b.position, u);
                    target.rotation = Quaternion.Slerp(a.Rotation, b.Rotation, u);
                    return;
                }
            }
        }

        private static void SetActive(Transform t, bool active)
        {
            if (t != null) t.gameObject.SetActive(active);
        }

        // Quick scale-up/down pulse on the opponent to sell the intercept moment.
        private static IEnumerator InterceptPulse(Transform npc, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector3 baseScale = npc.localScale;
            Vector3 bigScale  = baseScale * 1.25f;
            float half = 0.12f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                npc.localScale = Vector3.Lerp(baseScale, bigScale, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                npc.localScale = Vector3.Lerp(bigScale, baseScale, t / half);
                yield return null;
            }
            npc.localScale = baseScale;
        }

        void OnDisable()
        {
            // Safety: never leave timeScale modified if disabled mid-playback.
            if (IsPlaying) Time.timeScale = 1f;
        }
    }
}
