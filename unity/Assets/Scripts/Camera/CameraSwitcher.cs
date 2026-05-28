// CameraSwitcher.cs — Switch between multiple camera views.
// Press C to cycle cameras, or 1/2/3 to jump to direct-select cameras.
// Uses the New Input System (Keyboard.current) so input survives focus loss
// in Unity 6's "Both" Active Input Handling mode.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    public class CameraSwitcher : MonoBehaviour
    {
        [Header("Cameras")]
        [Tooltip("Cameras in order of cycling. First one is default.")]
        [SerializeField] private List<Camera> _cameras = new List<Camera>();

        [Header("Direct-Select Cameras (1/2/3/4)")]
        [Tooltip("Camera names bound to keys Digit1-4. Resolved at Start if not assigned.")]
        [SerializeField] private string _key1Name = "OverheadCam";
        [SerializeField] private string _key2Name = "SideCam";
        [SerializeField] private string _key3Name = "BehindRobotCam";
        [SerializeField] private string _key4Name = "FpsCamera";
        [SerializeField] private Camera _key1Camera;
        [SerializeField] private Camera _key2Camera;
        [SerializeField] private Camera _key3Camera;
        [SerializeField] private Camera _key4Camera;

        private int _currentIndex = 0;

        void Start()
        {
            // If no cameras assigned, find all in scene
            if (_cameras.Count == 0)
            {
                _cameras.AddRange(FindObjectsByType<Camera>(FindObjectsSortMode.None));
            }

            // Auto-resolve direct-select cameras by name if not assigned
            if (_key1Camera == null) _key1Camera = FindByName(_key1Name);
            if (_key2Camera == null) _key2Camera = FindByName(_key2Name);
            if (_key3Camera == null) _key3Camera = FindByName(_key3Name);
            if (_key4Camera == null) _key4Camera = FindByName(_key4Name);

            // Enable only the first camera
            for (int i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i] != null)
                {
                    _cameras[i].enabled = (i == 0);
                    // Disable audio listener on non-main cameras
                    if (i > 0)
                    {
                        var listener = _cameras[i].GetComponent<AudioListener>();
                        if (listener != null) listener.enabled = false;
                    }
                }
            }
        }

        private Camera FindByName(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            foreach (var c in _cameras) if (c != null && c.name == n) return c;
            // Fall back to a scene-wide search
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.name == n) return c;
            return null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.cKey.wasPressedThisFrame) SwitchCamera();

            // Direct-select via 1 / 2 / 3 / 4
            if (kb.digit1Key.wasPressedThisFrame) SwitchToCamera(_key1Camera);
            if (kb.digit2Key.wasPressedThisFrame) SwitchToCamera(_key2Camera);
            if (kb.digit3Key.wasPressedThisFrame) SwitchToCamera(_key3Camera);
            if (kb.digit4Key.wasPressedThisFrame) SwitchToCamera(_key4Camera);
        }

        private void SwitchToCamera(Camera target)
        {
            if (target == null) return;
            int idx = _cameras.IndexOf(target);
            if (idx < 0)
            {
                // Not registered yet — add and continue
                _cameras.Add(target);
                idx = _cameras.Count - 1;
                var al = target.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
            if (_cameras[_currentIndex] != null)
                _cameras[_currentIndex].enabled = false;
            _currentIndex = idx;
            target.enabled = true;
        }

        /// <summary>Switch to the next camera in the list.</summary>
        public void SwitchCamera()
        {
            if (_cameras.Count < 2) return;

            // Disable current
            if (_cameras[_currentIndex] != null)
                _cameras[_currentIndex].enabled = false;

            // Next index
            _currentIndex = (_currentIndex + 1) % _cameras.Count;

            // Enable next
            if (_cameras[_currentIndex] != null)
                _cameras[_currentIndex].enabled = true;

            Debug.Log($"[CameraSwitcher] Camera {_currentIndex}: {_cameras[_currentIndex]?.name}");
        }

        /// <summary>Switch to a specific camera by name.</summary>
        public void SwitchTo(string cameraName)
        {
            for (int i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i] != null && _cameras[i].name == cameraName)
                {
                    if (_cameras[_currentIndex] != null)
                        _cameras[_currentIndex].enabled = false;
                    _currentIndex = i;
                    _cameras[_currentIndex].enabled = true;
                    return;
                }
            }
            Debug.LogWarning($"[CameraSwitcher] Camera '{cameraName}' not found.");
        }
    }
}
