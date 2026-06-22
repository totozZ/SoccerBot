using System.Collections;
using UnityEngine;

namespace SoccerBot
{
    [DisallowMultipleComponent]
    public class NpcAnimationPresenter : MonoBehaviour
    {
        public enum NpcRole { FieldPlayer, Goalkeeper }
        public enum LocomotionState { Unknown, Idle, Walk, Run }

        private const string IdleState = "idle";
        private const string WalkState = "walk";
        private const string RunState = "run";
        private const string CelebrateState = "wave";

        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private NpcRole _role;

        [Header("Locomotion")]
        [SerializeField, Min(0f)] private float _walkThreshold = 0.08f;
        [SerializeField, Min(0f)] private float _runThreshold = 0.9f;
        [SerializeField, Min(0.1f)] private float _speedSmoothing = 12f;
        [SerializeField, Min(0.1f)] private float _teleportDistance = 1.5f;
        [SerializeField, Min(0f)] private float _crossFadeDuration = 0.12f;

        [Header("Procedural Fallback")]
        [SerializeField, Min(0f)] private float _idleBreathHeight = 0.012f;
        [SerializeField, Min(0f)] private float _movingBobHeight = 0.035f;
        [SerializeField, Min(0f)] private float _movingBobFrequency = 8f;
        [SerializeField, Range(0f, 12f)] private float _movingLeanAngle = 5f;

        [Header("Goalkeeper Save")]
        [SerializeField, Min(0f)] private float _saveDiveDistance = 0.48f;
        [SerializeField, Min(0f)] private float _saveDiveLift = 0.14f;
        [SerializeField, Range(10f, 85f)] private float _saveDiveAngle = 62f;
        [SerializeField, Min(0.01f)] private float _saveDiveDuration = 0.16f;
        [SerializeField, Min(0f)] private float _saveHoldDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float _saveRecoverDuration = 0.28f;

        private Vector3 _baseVisualLocalPosition;
        private Quaternion _baseVisualLocalRotation;
        private Vector3 _lastWorldPosition;
        private float _smoothedSpeed;
        private float _specialStateUntil;
        private bool _hasLastWorldPosition;
        private LocomotionState _locomotionState = LocomotionState.Unknown;
        private Coroutine _saveRoutine;

        public NpcRole Role => _role;
        public float SmoothedSpeed => _smoothedSpeed;
        public bool HasAnimator => _animator != null && _animator.runtimeAnimatorController != null;

        public void Configure(NpcRole role)
        {
            _role = role;
            ResolveReferences();
            CaptureVisualBase();

            if (_animator != null)
                _animator.applyRootMotion = false;

            _locomotionState = LocomotionState.Unknown;
            ApplyLocomotion(LocomotionState.Idle);
        }

        private void Awake()
        {
            ResolveReferences();
            CaptureVisualBase();
        }

        private void OnEnable()
        {
            _lastWorldPosition = transform.position;
            _hasLastWorldPosition = true;
            _locomotionState = LocomotionState.Unknown;
        }

        private void OnDisable()
        {
            if (_saveRoutine != null)
            {
                StopCoroutine(_saveRoutine);
                _saveRoutine = null;
            }

            RestoreVisualBase();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            Vector3 currentPosition = transform.position;
            float rawSpeed = 0f;

            if (_hasLastWorldPosition && deltaTime > 0.0001f)
            {
                float distance = Vector3.ProjectOnPlane(currentPosition - _lastWorldPosition, Vector3.up).magnitude;
                if (distance <= _teleportDistance)
                    rawSpeed = distance / deltaTime;
            }

            _lastWorldPosition = currentPosition;
            _hasLastWorldPosition = true;

            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, _speedSmoothing) * deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, blend);

            if (_saveRoutine != null || Time.time < _specialStateUntil)
                return;

            LocomotionState desired = ClassifyLocomotion(_smoothedSpeed, _walkThreshold, _runThreshold);
            ApplyLocomotion(desired);

            if (!HasAnimator)
                ApplyProceduralFallback(desired);
        }

        public void TriggerSave(Vector3 worldDirection)
        {
            if (_role != NpcRole.Goalkeeper || _visualRoot == null || !isActiveAndEnabled)
                return;

            if (_saveRoutine != null)
                StopCoroutine(_saveRoutine);

            RestoreVisualBase();
            _saveRoutine = StartCoroutine(PlaySave(worldDirection));
        }

        public void TriggerCelebrate(float duration = 1.2f)
        {
            if (!isActiveAndEnabled || _saveRoutine != null)
                return;

            _specialStateUntil = Time.time + Mathf.Max(0.1f, duration);
            _locomotionState = LocomotionState.Unknown;
            TryPlayAnimatorState(CelebrateState, 0.1f);
        }

        private IEnumerator PlaySave(Vector3 worldDirection)
        {
            _specialStateUntil = Time.time +
                                 _saveDiveDuration +
                                 _saveHoldDuration +
                                 _saveRecoverDuration;
            _locomotionState = LocomotionState.Unknown;
            TryPlayAnimatorState(RunState, 0.05f);

            Vector3 flatDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            float side = flatDirection.sqrMagnitude > 0.0001f &&
                         Vector3.Dot(flatDirection.normalized, transform.right) < 0f
                ? -1f
                : 1f;

            Vector3 divePosition = _baseVisualLocalPosition +
                                   Vector3.right * (side * _saveDiveDistance) +
                                   Vector3.up * _saveDiveLift;
            Quaternion diveRotation = _baseVisualLocalRotation *
                                      Quaternion.Euler(0f, 0f, -side * _saveDiveAngle);

            yield return AnimateVisual(
                _baseVisualLocalPosition,
                divePosition,
                _baseVisualLocalRotation,
                diveRotation,
                _saveDiveDuration,
                true);
            yield return new WaitForSeconds(_saveHoldDuration);
            yield return AnimateVisual(
                divePosition,
                _baseVisualLocalPosition,
                diveRotation,
                _baseVisualLocalRotation,
                _saveRecoverDuration,
                false);

            RestoreVisualBase();
            _saveRoutine = null;
            _locomotionState = LocomotionState.Unknown;
        }

        private IEnumerator AnimateVisual(
            Vector3 fromPosition,
            Vector3 toPosition,
            Quaternion fromRotation,
            Quaternion toRotation,
            float duration,
            bool easeOut)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = easeOut
                    ? 1f - (1f - t) * (1f - t)
                    : t * t * (3f - 2f * t);
                _visualRoot.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, eased);
                _visualRoot.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, eased);
                yield return null;
            }
        }

        private void ApplyLocomotion(LocomotionState state)
        {
            if (_locomotionState == state)
                return;

            _locomotionState = state;
            string stateName = state switch
            {
                LocomotionState.Run => RunState,
                LocomotionState.Walk => WalkState,
                _ => IdleState
            };

            TryPlayAnimatorState(stateName, _crossFadeDuration);
        }

        private bool TryPlayAnimatorState(string stateName, float transitionDuration)
        {
            if (!HasAnimator)
                return false;

            int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (!_animator.HasState(0, fullPathHash))
                return false;

            _animator.CrossFadeInFixedTime(fullPathHash, Mathf.Max(0f, transitionDuration), 0);
            return true;
        }

        private void ApplyProceduralFallback(LocomotionState state)
        {
            if (_visualRoot == null || _visualRoot == transform)
                return;

            float phase = Time.time * Mathf.Max(0.1f, _movingBobFrequency);
            float bobHeight = state == LocomotionState.Idle ? _idleBreathHeight : _movingBobHeight;
            float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight;
            float lean = state == LocomotionState.Idle ? 0f : Mathf.Sin(phase * 0.5f) * _movingLeanAngle;

            _visualRoot.localPosition = _baseVisualLocalPosition + Vector3.up * bob;
            _visualRoot.localRotation = _baseVisualLocalRotation * Quaternion.Euler(lean, 0f, 0f);
        }

        private void ResolveReferences()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_visualRoot == null && _animator != null)
                _visualRoot = _animator.transform;

            if (_visualRoot == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.GetComponentInChildren<Renderer>(true) == null)
                        continue;

                    _visualRoot = child;
                    break;
                }
            }
        }

        private void CaptureVisualBase()
        {
            if (_visualRoot == null)
                return;

            _baseVisualLocalPosition = _visualRoot.localPosition;
            _baseVisualLocalRotation = _visualRoot.localRotation;
        }

        private void RestoreVisualBase()
        {
            if (_visualRoot == null)
                return;

            _visualRoot.localPosition = _baseVisualLocalPosition;
            _visualRoot.localRotation = _baseVisualLocalRotation;
        }

        public static LocomotionState ClassifyLocomotion(
            float speed,
            float walkThreshold = 0.08f,
            float runThreshold = 0.9f)
        {
            float safeWalkThreshold = Mathf.Max(0f, walkThreshold);
            float safeRunThreshold = Mathf.Max(safeWalkThreshold, runThreshold);
            float safeSpeed = Mathf.Max(0f, speed);

            if (safeSpeed < safeWalkThreshold)
                return LocomotionState.Idle;
            if (safeSpeed < safeRunThreshold)
                return LocomotionState.Walk;
            return LocomotionState.Run;
        }
    }
}
