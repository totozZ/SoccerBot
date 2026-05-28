// MatchFlowController.cs — One-shot match loop driver.
//
// Phase order:
//   Setup       ball pinned to robot, NPCs hidden
//   Pass        ball flies parabolically from robot to player's foot zone
//   Possession  ball pinned to player, FPSPlayerController.ShootingEnabled = true
//   Shot        player released LMB → pick scenario by power, ScenarioTrigger.ForcePlay
//   Score       waits for ScenarioPlayer.OnScenarioComplete + ScorePanel display
//   Cooldown    fixed pause, then back to Setup
//
// Power → scenario routing (with optional ±jitter):
//   power >= _scoreThreshold (0.7) → index 0 (ScoreSuccess)
//   power >= _missThreshold  (0.4) → index 2 (ShotMissed)
//   else                            → index 1 (Intercepted)
//
// Indices match the ScenarioTrigger._scenarios list order configured in scene.

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
        [Tooltip("Index 0 in ScenarioTrigger's list (ScoreSuccess) when power >= this.")]
        [SerializeField, Range(0.5f, 1f)] private float _scoreThreshold = 0.7f;
        [Tooltip("Index 2 (ShotMissed) when power >= this; otherwise index 1 (Intercepted).")]
        [SerializeField, Range(0.1f, 0.7f)] private float _missThreshold = 0.4f;
        [SerializeField, Range(0f, 0.3f)] private float _randomJitter = 0.10f;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;

        private Coroutine _loop;
        private Vector3 _camRestPos;          // P7.2 fix: cached camera world pose at shot moment
        private Quaternion _camRestRot;

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
            if (_ball != null) _ball.Detach();          // let the scenario take over

            // ★ Bug 1 fix: detach FpsCamera so the player keeps watching from where
            // they fired, rather than the camera tracking Player or Teammate.
            if (_fpsCamera != null)
            {
                _camRestPos = _fpsCamera.position;
                _camRestRot = _fpsCamera.rotation;
                _fpsCamera.SetParent(null, true);
            }

            // ★ Bug 3 fix: ScenarioPlayer's pre-baked +Z trajectory now anchors to
            // Player's transform — and Player.rotation already equals the camera yaw
            // (FPSPlayerController writes yaw to transform.rotation), so the ball
            // flies wherever the player was aiming. No extra rotation work needed.
            if (_scenarioPlayer != null) _scenarioPlayer.SetOrigin(_playerTransform);

            float effectivePower = Mathf.Clamp01(power01 + Random.Range(-_randomJitter, _randomJitter));
            int idx;
            if (effectivePower >= _scoreThreshold)      idx = 0;   // ScoreSuccess
            else if (effectivePower >= _missThreshold)  idx = 2;   // ShotMissed
            else                                        idx = 1;   // Intercepted

            Debug.Log($"[MatchFlow] Shot power={power01:F2} (eff={effectivePower:F2}) → scenario[{idx}]");

            if (_scenarioTrigger != null) _scenarioTrigger.ForcePlay(idx);
        }

        private void HandleScenarioComplete(Scenario s)
        {
            if (CurrentPhase != Phase.Shot) return;
            CurrentPhase = Phase.Score;

            // Reparent camera back to FpsAnchor so FPSPlayerController's look
            // controls work again on the next round.
            if (_fpsCamera != null && _fpsAnchor != null)
            {
                _fpsCamera.SetParent(_fpsAnchor, true);
                _fpsCamera.localPosition = Vector3.zero;
                _fpsCamera.localRotation = Quaternion.identity;
            }
        }
    }
}
