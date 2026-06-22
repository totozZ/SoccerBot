using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SoccerBot
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameplayModeBootstrap : MonoBehaviour
    {
        private const string ModeKey = "SoccerBot.GameplayMode";
        private const string ProfileKey = "SoccerBot.ControlProfile";

        public static GameplayMode CurrentMode { get; private set; } = GameplayMode.ArenaAttack;
        public static ControlProfile CurrentProfile { get; private set; } = ControlProfile.KeyboardMouse;

        private bool _reloading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForGameplayScene()
        {
            if (FindFirstObjectByType<MatchFlowController>(FindObjectsInactive.Include) == null)
                return;
            if (FindFirstObjectByType<GameplayModeBootstrap>(FindObjectsInactive.Include) != null)
                return;

            new GameObject("GameplayModeBootstrap").AddComponent<GameplayModeBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveLaunchSelection();
            ApplyModeToLoadedScene();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            _reloading = false;
            ApplyModeToLoadedScene();
        }

        private void Update()
        {
            if (_reloading || Keyboard.current == null)
                return;

            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                CurrentMode = CurrentMode == GameplayMode.ArenaAttack
                    ? GameplayMode.Training
                    : GameplayMode.ArenaAttack;
                PlayerPrefs.SetInt(ModeKey, (int)CurrentMode);
                ReloadScene();
                return;
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                CurrentProfile = (ControlProfile)(((int)CurrentProfile + 1) % Enum.GetValues(typeof(ControlProfile)).Length);
                PlayerPrefs.SetInt(ProfileKey, (int)CurrentProfile);
                ReloadScene();
            }
        }

        private void ResolveLaunchSelection()
        {
            CurrentMode = (GameplayMode)Mathf.Clamp(
                PlayerPrefs.GetInt(ModeKey, (int)GameplayMode.ArenaAttack),
                0,
                Enum.GetValues(typeof(GameplayMode)).Length - 1);
            CurrentProfile = (ControlProfile)Mathf.Clamp(
                PlayerPrefs.GetInt(ProfileKey, (int)ControlProfile.KeyboardMouse),
                0,
                Enum.GetValues(typeof(ControlProfile)).Length - 1);

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-training": CurrentMode = GameplayMode.Training; break;
                    case "-arena": CurrentMode = GameplayMode.ArenaAttack; break;
                    case "-gamepad": CurrentProfile = ControlProfile.Gamepad; break;
                    case "-vrstriker": CurrentProfile = ControlProfile.VrStriker; break;
                    case "-xrsimulator": CurrentProfile = ControlProfile.XrSimulator; break;
                }
            }
        }

        private void ApplyModeToLoadedScene()
        {
            MatchFlowController trainingFlow = FindFirstObjectByType<MatchFlowController>(FindObjectsInactive.Include);
            if (trainingFlow == null)
                return;

            bool arena = CurrentMode == GameplayMode.ArenaAttack;
            trainingFlow.enabled = !arena;

            FieldAIController trainingAI = FindFirstObjectByType<FieldAIController>(FindObjectsInactive.Include);
            if (trainingAI != null)
                trainingAI.enabled = !arena;

            FPSPlayerController legacyPlayer = FindFirstObjectByType<FPSPlayerController>(FindObjectsInactive.Include);
            if (legacyPlayer != null)
                legacyPlayer.enabled = !arena;

            if (!arena)
                return;

            if (FindFirstObjectByType<ArenaAttackController>(FindObjectsInactive.Include) != null)
                return;
            GameObject root = new GameObject("ArenaAttackRuntime");
            root.AddComponent<ArenaAttackController>();
        }

        private void ReloadScene()
        {
            _reloading = true;
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid())
                SceneManager.LoadScene(active.buildIndex);
        }
    }
}
