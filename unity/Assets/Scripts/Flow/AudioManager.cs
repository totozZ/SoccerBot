using System.Collections;
using UnityEngine;

namespace SoccerBot
{
    // Minimal BGM + SFX manager. No audio clips are bundled — wire them in Inspector
    // or drop them into Resources/Audio/ and set the resource paths below.
    //
    // BGM fades between tracks as MatchFlowController phases change.
    // SFX are one-shot plays triggered by game events.
    //
    // Add this component to any persistent GameObject (e.g. GameManager).
    public class AudioManager : MonoBehaviour
    {
        [Header("BGM Sources (two for crossfade)")]
        [SerializeField] private AudioSource _bgmA;
        [SerializeField] private AudioSource _bgmB;

        [Header("SFX Source")]
        [SerializeField] private AudioSource _sfxSource;

        [Header("BGM Clips")]
        [SerializeField] private AudioClip _introBGM;
        [SerializeField] private AudioClip _matchBGM;
        [SerializeField] private AudioClip _replayBGM;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip _sfxShoot;
        [SerializeField] private AudioClip _sfxGoal;
        [SerializeField] private AudioClip _sfxIntercept;
        [SerializeField] private AudioClip _sfxMiss;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume    = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume    = 1f;

        [Header("Crossfade")]
        [SerializeField] private float _crossfadeTime = 1.2f;

        [Header("References")]
        [SerializeField] private MatchFlowController _matchFlow;

        private AudioSource _activeBgm;
        private Coroutine   _fadeRoutine;
        private MatchFlowController.Phase _lastPhase = MatchFlowController.Phase.Idle;

        // Backstop against a duplicate AudioManager sneaking back into the scene
        // (the editor Wirer dedups at author time; this guards at runtime).
        private static AudioManager _instance;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        void Start()
        {
            if (_matchFlow == null)
                _matchFlow = FindFirstObjectByType<MatchFlowController>();

            EnsureSources();
            _activeBgm = _bgmA;

            // Start with intro BGM if available
            PlayBGM(_introBGM, instant: true);
        }

        void Update()
        {
            if (_matchFlow == null) return;
            var phase = _matchFlow.CurrentPhase;
            if (phase != _lastPhase)
            {
                OnPhaseChanged(phase);
                _lastPhase = phase;
            }
        }

        // ── Public SFX API ───────────────────────────────────

        public void PlayShoot()     => PlaySFX(_sfxShoot);
        public void PlayGoal()      => PlaySFX(_sfxGoal);
        public void PlayIntercept() => PlaySFX(_sfxIntercept);
        public void PlayMiss()      => PlaySFX(_sfxMiss);

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, _sfxVolume * _masterVolume);
        }

        // ── BGM ──────────────────────────────────────────────

        public void PlayBGM(AudioClip clip, bool instant = false)
        {
            if (clip == null) return;
            if (_activeBgm != null && _activeBgm.clip == clip && _activeBgm.isPlaying) return;

            if (instant)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _activeBgm.clip   = clip;
                _activeBgm.volume = _bgmVolume * _masterVolume;
                _activeBgm.loop   = true;
                _activeBgm.Play();
                return;
            }

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Crossfade(clip));
        }

        public void StopBGM()
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOut(_activeBgm, _crossfadeTime));
        }

        // ── Phase-driven BGM switching ───────────────────────

        private void OnPhaseChanged(MatchFlowController.Phase to)
        {
            switch (to)
            {
                case MatchFlowController.Phase.Setup:
                case MatchFlowController.Phase.Pass:
                case MatchFlowController.Phase.Possession:
                    PlayBGM(_matchBGM);
                    break;

                case MatchFlowController.Phase.Shot:
                    PlaySFX(_sfxShoot);
                    break;

                case MatchFlowController.Phase.Score:
                    PlayBGM(_replayBGM);
                    break;

                case MatchFlowController.Phase.Cooldown:
                    // Let replay BGM finish naturally; it will crossfade back on next Setup
                    break;
            }
        }

        // ── Coroutines ───────────────────────────────────────

        private IEnumerator Crossfade(AudioClip newClip)
        {
            // Pick the inactive source as the incoming track
            AudioSource incoming = (_activeBgm == _bgmA) ? _bgmB : _bgmA;
            AudioSource outgoing = _activeBgm;

            incoming.clip   = newClip;
            incoming.volume = 0f;
            incoming.loop   = true;
            incoming.Play();

            float t = 0f;
            float targetVol = _bgmVolume * _masterVolume;
            while (t < _crossfadeTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / _crossfadeTime);
                incoming.volume = Mathf.Lerp(0f, targetVol, u);
                outgoing.volume = Mathf.Lerp(targetVol, 0f, u);
                yield return null;
            }
            outgoing.Stop();
            outgoing.volume = 0f;
            _activeBgm = incoming;
        }

        private IEnumerator FadeOut(AudioSource src, float duration)
        {
            float startVol = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / duration));
                yield return null;
            }
            src.Stop();
            src.volume = 0f;
        }

        // ── Helpers ──────────────────────────────────────────

        private void EnsureSources()
        {
            if (_bgmA == null)
            {
                _bgmA = gameObject.AddComponent<AudioSource>();
                _bgmA.playOnAwake = false;
                _bgmA.loop = true;
            }
            if (_bgmB == null)
            {
                _bgmB = gameObject.AddComponent<AudioSource>();
                _bgmB.playOnAwake = false;
                _bgmB.loop = true;
            }
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
            }
        }
    }
}
