// MatchFlowController.cs — One-shot match loop driver.
//
// Phase order:
//   Setup       ball pinned to robot, NPCs stand on the field next to the player
//   Pass        ball flies parabolically from robot to player's foot zone
//   Possession  ball pinned to player, FPSPlayerController.ShootingEnabled = true
//   Shot        player released LMB → DoTeammateShot or scenario based on power
//   Score       ScorePanel displays, camera reparents
//   Cooldown    fixed pause, then back to Setup
//
// Power → outcome routing (with optional ±jitter):
//   power >= _scoreThreshold (0.7) → DoTeammateShot to GoalTarget_In   (Score)
//   power >= _missThreshold  (0.4) → DoTeammateShot to GoalTarget_Miss (Missed)
//   else                            → ScenarioTrigger.ForcePlay(1)     (Intercepted)
//
// Score and Miss are driven directly by MatchFlow (Lerp + parabolic Y) so the ball
// always reaches the goal area. Intercepted falls back to ScenarioPlayer because
// NPC interactions are richer there.

using System.Collections;
using UnityEngine;

namespace SoccerBot
{
    public class MatchFlowController : MonoBehaviour
    {
        public enum Phase { Idle, Setup, Pass, Possession, Shot, Score, Cooldown }

        [Header("Scene References")]
        [SerializeField] private Transform _robotTransform;
        [SerializeField] private Transform _playerTransform;        // The Player GameObject (camera anchor)
        [SerializeField] private Transform _teammateTransform;      // The blue NPC driven by ScenarioPlayer
        [SerializeField] private Transform _opponentTransform;      // The red NPC driven by ScenarioPlayer
        [SerializeField] private BallController _ball;
        [SerializeField] private FPSPlayerController _player;
        [SerializeField] private ScenarioTrigger _scenarioTrigger;
        [SerializeField] private ScenarioPlayer _scenarioPlayer;
        [SerializeField] private Transform _fpsCamera;              // Player/FpsAnchor/FpsCamera, detached during scenario
        [SerializeField] private Transform _fpsAnchor;              // Player/FpsAnchor, reparent target after scenario

        [Header("Ball Offsets")]
        [SerializeField] private Vector3 _ballOffsetRobot   = new Vector3(0f, 1.0f, 0.4f);   // hands of robot
        [SerializeField] private Vector3 _ballOffsetPlayer  = new Vector3(0f, 0.3f, 0.6f);   // foot zone in front
        [SerializeField] private float _passApex = 1.5f;                                     // peak height during pass

        [Header("NPC Setup Stance (Player local-space)")]
        [Tooltip("Where Teammate stands relative to Player during Setup/Pass/Possession so they don't pop in at shot time.")]
        [SerializeField] private Vector3 _teammateSetupOffset = new Vector3(-1.5f, 0f, 1.5f);
        [Tooltip("Where Opponent stands relative to Player during Setup/Pass/Possession.")]
        [SerializeField] private Vector3 _opponentSetupOffset = new Vector3( 1.5f, 0f, 1.5f);

        [Header("Timing (seconds)")]
        [SerializeField] private float _setupDuration   = 1.5f;
        [SerializeField] private float _passFlightTime  = 1.0f;
        [SerializeField] private float _cooldownDuration = 3.0f;

        [Header("Power Routing")]
        [Tooltip("Index 0 in ScenarioTrigger's list (Intercepted) when power < _missThreshold.")]
        [SerializeField, Range(0.5f, 1f)] private float _scoreThreshold = 0.7f;
        [Tooltip("Index 2 (ShotMissed) when power >= this; otherwise index 1 (Intercepted).")]
        [SerializeField, Range(0.1f, 0.7f)] private float _missThreshold = 0.4f;
        [SerializeField, Range(0f, 0.3f)] private float _randomJitter = 0.10f;

        [Header("Teammate Shot Targets (world space)")]
        [Tooltip("Optional. If wired, used as the in-goal target. Otherwise falls back to _goalTargetInPos.")]
        [SerializeField] private Transform _goalTargetIn;
        [Tooltip("Optional. If wired, used as the miss target. Otherwise falls back to _goalTargetMissPos.")]
        [SerializeField] private Transform _goalTargetMiss;
        [SerializeField] private Vector3 _goalTargetInPos   = new Vector3(0f,   0.8f, -9.5f);
        [SerializeField] private Vector3 _goalTargetMissPos = new Vector3(3.5f, 0.5f, -9.5f);

        [Header("Teammate Shot Animation")]
        [SerializeField] private float _shotPassToTeammate = 0.6f;     // ball flight time from player to teammate's foot
        [SerializeField] private float _teammateAimDuration = 0.3f;     // teammate yaw rotation time
        [SerializeField] private float _teammateAimHold     = 0.2f;     // hold after aiming, before kick
        [SerializeField] private float _shotFlightTime = 1.4f;          // ball flight time from teammate to goal
        [SerializeField] private float _shotApex = 2.5f;                // peak height of the kick arc
        [SerializeField] private float _teammateRunDistance = 2.0f;     // forward dash distance during the kick
        [SerializeField] private float _teammateRunFraction = 0.3f;     // dash completes within this fraction of _shotFlightTime
        [SerializeField] private float _resultHoldDelay = 0.3f;         // pause after ball hits target before showing ScorePanel

        [Header("Whistle")]
        [SerializeField] private AudioSource _whistleSource;

        [Header("Score Display Data (wire 2 .asset files)")]
        [Tooltip("Wire ScoreSuccess.asset — used as data carrier for ScorePanel when player scores.")]
        [SerializeField] private Scenario _scoreSuccessData;
        [Tooltip("Wire ShotMissed.asset — used as data carrier for ScorePanel when player misses.")]
        [SerializeField] private Scenario _shotMissedData;
        [SerializeField] private ScorePanel _scorePanel;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;

        private Coroutine _loop;

        void Start()
        {
            AutoResolveRefs();

            if (_player != null) _player.OnShoot += HandlePlayerShot;
            if (_scenarioPlayer != null) _scenarioPlayer.OnScenarioComplete += HandleScenarioComplete;

            _loop = StartCoroutine(MatchLoop());
        }

        // Self-wire any unassigned fields by name / type so the scene only needs
        // the components added — no manual Inspector dragging required.
        // Player/Teammate refs override stale serialized values (the previous
        // architecture pointed _playerTransform at "Teammate") so the new layout
        // takes effect even without re-saving the scene.
        private void AutoResolveRefs()
        {
            if (_robotTransform == null)
            {
                var go = GameObject.Find("Robot");
                if (go != null) _robotTransform = go.transform;
            }
            // Always resolve Player by name — overrides any stale ref to Teammate.
            {
                var go = GameObject.Find("Player");
                if (go != null) _playerTransform = go.transform;
            }
            // Always resolve Teammate by name.
            {
                var go = GameObject.Find("Teammate");
                if (go != null) _teammateTransform = go.transform;
            }
            // Always resolve Opponent by name. Note: GameObject.Find skips
            // inactive root GOs — the scene saves Opponent as inactive, so this
            // may return null. Fall back to ScenarioPlayer's wired ref below.
            if (_opponentTransform == null)
            {
                var go = GameObject.Find("Opponent");
                if (go != null) _opponentTransform = go.transform;
            }
            if (_ball == null) _ball = FindFirstObjectByType<BallController>();
            // Always re-resolve player controller from the Player GO so we never
            // bind to Teammate's disabled controller.
            if (_playerTransform != null)
            {
                var pc = _playerTransform.GetComponent<FPSPlayerController>();
                if (pc != null) _player = pc;
            }
            if (_player == null) _player = FindFirstObjectByType<FPSPlayerController>();
            if (_scenarioTrigger == null) _scenarioTrigger = FindFirstObjectByType<ScenarioTrigger>();
            if (_scenarioPlayer == null) _scenarioPlayer = FindFirstObjectByType<ScenarioPlayer>();
            // Borrow NPC refs from ScenarioPlayer (covers inactive Opponent that Find can't reach).
            if (_opponentTransform == null && _scenarioPlayer != null)
                _opponentTransform = _scenarioPlayer.OpponentTransform;
            if (_teammateTransform == null && _scenarioPlayer != null)
                _teammateTransform = _scenarioPlayer.TeammateTransform;
            // Always re-resolve FpsAnchor / FpsCamera under the new Player tree.
            if (_playerTransform != null)
            {
                var t = _playerTransform.Find("FpsAnchor");
                if (t != null) _fpsAnchor = t;
            }
            if (_fpsAnchor != null)
            {
                var t = _fpsAnchor.Find("FpsCamera");
                if (t != null) _fpsCamera = t;
            }
            // Goal target markers (optional). Fall back to default Vector3 if missing.
            if (_goalTargetIn == null)
            {
                var go = GameObject.Find("GoalTarget_In");
                if (go != null) _goalTargetIn = go.transform;
            }
            if (_goalTargetMiss == null)
            {
                var go = GameObject.Find("GoalTarget_Miss");
                if (go != null) _goalTargetMiss = go.transform;
            }
            if (_scorePanel == null) _scorePanel = FindFirstObjectByType<ScorePanel>();
        }

        private void PlayWhistle()
        {
            if (_whistleSource == null)
            {
                _whistleSource = gameObject.AddComponent<AudioSource>();
                _whistleSource.playOnAwake = false;
                _whistleSource.spatialBlend = 0f;
            }
            var clip = WhistleGenerator.Create();
            _whistleSource.PlayOneShot(clip, 0.8f);
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnShoot -= HandlePlayerShot;
            if (_scenarioPlayer != null) _scenarioPlayer.OnScenarioComplete -= HandleScenarioComplete;
        }

        // ── Loop ─────────────────────────────────────────────

        private IEnumerator MatchLoop()
        {
            while (true)
            {
                yield return DoSetup();
                yield return DoPass();
                yield return DoPossession();      // exits when player shoots
                yield return DoShotAndScore();    // exits when scenario complete
                yield return DoCooldown();
            }
        }

        private IEnumerator DoSetup()
        {
            CurrentPhase = Phase.Setup;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
            }
            if (_teammateTransform != null && _playerTransform != null)
            {
                _teammateTransform.gameObject.SetActive(true);
                _teammateTransform.position = _playerTransform.TransformPoint(_teammateSetupOffset);
                _teammateTransform.rotation = _playerTransform.rotation;
            }
            if (_opponentTransform != null && _playerTransform != null)
            {
                _opponentTransform.gameObject.SetActive(true);
                _opponentTransform.position = _playerTransform.TransformPoint(_opponentSetupOffset);
                Vector3 toPlayer = _playerTransform.position - _opponentTransform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                    _opponentTransform.rotation = Quaternion.LookRotation(toPlayer);
            }
            if (_ball != null && _robotTransform != null)
                _ball.AttachTo(_robotTransform, _ballOffsetRobot);
            yield return new WaitForSeconds(_setupDuration);
        }

        private IEnumerator DoPass()
        {
            CurrentPhase = Phase.Pass;

            // Play whistle at the start of each pass
            PlayWhistle();

            if (_ball == null || _robotTransform == null || _playerTransform == null)
            {
                yield return null;
                yield break;
            }

            _ball.Detach();
            Vector3 startPos = _robotTransform.TransformPoint(_ballOffsetRobot);
            Vector3 endPos   = _playerTransform.TransformPoint(_ballOffsetPlayer);

            float t = 0f;
            while (t < _passFlightTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _passFlightTime);
                Vector3 pos = Vector3.Lerp(startPos, endPos, u);
                pos.y += _passApex * 4f * u * (1f - u);    // parabolic arc, peaks at u=0.5
                _ball.transform.position = pos;
                yield return null;
            }
            _ball.transform.position = endPos;
        }

        private IEnumerator DoPossession()
        {
            CurrentPhase = Phase.Possession;
            if (_ball != null && _playerTransform != null)
                _ball.AttachTo(_playerTransform, _ballOffsetPlayer);
            if (_player != null)
            {
                _player.ShootingEnabled = true;
                _player.MovementEnabled = true;
            }

            // Hold here until HandlePlayerShot flips the phase to Shot.
            while (CurrentPhase == Phase.Possession) yield return null;
        }

        private IEnumerator DoShotAndScore()
        {
            // Phase already set to Shot by HandlePlayerShot.
            // Wait for OnScenarioComplete to flip us to Score, then bail.
            float timeout = 12f;     // safety in case scenario never fires complete
            float t = 0f;
            while (CurrentPhase != Phase.Score && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator DoCooldown()
        {
            CurrentPhase = Phase.Cooldown;
            yield return new WaitForSeconds(_cooldownDuration);
        }

        // ── Event handlers ───────────────────────────────────

        private void HandlePlayerShot(float power01, Vector3 direction)
        {
            if (CurrentPhase != Phase.Possession) return;

            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
            }
            if (_ball != null) _ball.Detach();

            // The FPS camera stays parented to Player/FpsAnchor through the shot.
            // The player doesn't move during Shot, so the view holds where they fired —
            // and on Quest the headset's TrackedPoseDriver keeps driving head rotation
            // unobstructed (detaching used to fight it and flipped the view 180°).

            float effectivePower = Mathf.Clamp01(power01 + Random.Range(-_randomJitter, _randomJitter));

            // Score / Missed → MatchFlow drives the ball directly via DoTeammateShot.
            // Intercepted (low power) → fall back to ScenarioPlayer (NPC interactions
            // are richer there and don't need precise targeting).
            if (effectivePower >= _scoreThreshold)
            {
                Debug.Log($"[MatchFlow] Shot power={power01:F2} (eff={effectivePower:F2}) → SCORE");
                Vector3 target = _goalTargetIn != null ? _goalTargetIn.position : _goalTargetInPos;
                StartCoroutine(DoTeammateShot(target, _scoreSuccessData));
            }
            else if (effectivePower >= _missThreshold)
            {
                Debug.Log($"[MatchFlow] Shot power={power01:F2} (eff={effectivePower:F2}) → MISS");
                Vector3 target = _goalTargetMiss != null ? _goalTargetMiss.position : _goalTargetMissPos;
                StartCoroutine(DoTeammateShot(target, _shotMissedData));
            }
            else
            {
                Debug.Log($"[MatchFlow] Shot power={power01:F2} (eff={effectivePower:F2}) → INTERCEPTED (scenario)");
                if (_scenarioPlayer != null) _scenarioPlayer.SetOrigin(_playerTransform);
                if (_scenarioTrigger != null) _scenarioTrigger.ForcePlay(0);
            }
        }

        // Teammate runs forward and kicks the ball to a target (Score or Miss).
        // Replaces the old ScenarioPlayer-driven Score/Missed paths so the ball
        // actually reaches the goal area rather than stopping short of design coords.
        private IEnumerator DoTeammateShot(Vector3 targetWorld, Scenario panelData)
        {
            // ─ Phase 1: ball arcs from current position (player's feet) to teammate's foot ─
            if (_ball != null && _teammateTransform != null)
            {
                Vector3 receivePos = _teammateTransform.position
                                    + new Vector3(0f, 0.3f, 0f)
                                    + _teammateTransform.forward * 0.4f;
                yield return ParabolicLerp(_ball.transform, _ball.transform.position, receivePos,
                                           _shotPassToTeammate, 0.8f);
            }

            // ─ Phase 2: teammate slerps to face the target ─
            if (_teammateTransform != null)
            {
                Vector3 toTarget = targetWorld - _teammateTransform.position; toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion startRot = _teammateTransform.rotation;
                    Quaternion targetRot = Quaternion.LookRotation(toTarget);
                    float rt = 0f;
                    while (rt < _teammateAimDuration)
                    {
                        rt += Time.deltaTime;
                        _teammateTransform.rotation = Quaternion.Slerp(
                            startRot, targetRot, Mathf.Clamp01(rt / _teammateAimDuration));
                        yield return null;
                    }
                    _teammateTransform.rotation = targetRot;
                }
            }
            yield return new WaitForSeconds(_teammateAimHold);

            // ─ Phase 3: teammate dashes forward while ball arcs to target ─
            Vector3 teammateStart = _teammateTransform != null ? _teammateTransform.position : Vector3.zero;
            Vector3 teammateEnd   = _teammateTransform != null
                ? teammateStart + _teammateTransform.forward * _teammateRunDistance
                : Vector3.zero;
            Vector3 ballStart = _ball != null ? _ball.transform.position : Vector3.zero;

            float t = 0f;
            float runWindow = Mathf.Max(0.01f, _shotFlightTime * _teammateRunFraction);
            while (t < _shotFlightTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _shotFlightTime);

                if (_teammateTransform != null)
                {
                    float runU = Mathf.Clamp01(t / runWindow);
                    _teammateTransform.position = Vector3.Lerp(teammateStart, teammateEnd, runU);
                }

                if (_ball != null)
                {
                    Vector3 pos = Vector3.Lerp(ballStart, targetWorld, u);
                    pos.y += _shotApex * 4f * u * (1f - u);
                    _ball.transform.position = pos;
                }
                yield return null;
            }
            if (_ball != null) _ball.transform.position = targetWorld;

            // ─ Phase 4: pause, then show score and resolve ─
            yield return new WaitForSeconds(_resultHoldDelay);
            if (_scorePanel != null && panelData != null) _scorePanel.Show(panelData);
            HandleShotResolved();
        }

        private IEnumerator ParabolicLerp(Transform target, Vector3 start, Vector3 end, float duration, float apex)
        {
            float u = 0f;
            while (u < duration)
            {
                u += Time.deltaTime;
                float p = Mathf.Clamp01(u / duration);
                Vector3 pos = Vector3.Lerp(start, end, p);
                pos.y += apex * 4f * p * (1f - p);
                target.position = pos;
                yield return null;
            }
            target.position = end;
        }

        // Shared wrap-up for both DoTeammateShot (code path) and HandleScenarioComplete
        // (Intercepted scenario path): flips phase to Score.
        private void HandleShotResolved()
        {
            if (CurrentPhase != Phase.Shot) return;
            CurrentPhase = Phase.Score;
        }

        private void HandleScenarioComplete(Scenario s)
        {
            // Intercepted scenario reaches us through this callback. Score/Missed
            // are handled by DoTeammateShot directly and never enter this path.
            HandleShotResolved();
        }
    }
}
