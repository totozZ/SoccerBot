// CameraSwitcher.cs — Switch between multiple camera views.
// Press C to cycle cameras, or assign to specific keys.

using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    public class CameraSwitcher : MonoBehaviour
    {
        [Header("Cameras")]
        [Tooltip("Cameras in order of cycling. First one is default.")]
        [SerializeField] private List<Camera> _cameras = new List<Camera>();

        [Header("Input")]
        [SerializeField] private KeyCode _switchKey = KeyCode.C;

        private int _currentIndex = 0;

        void Start()
        {
            // If no cameras assigned, find all in scene
            if (_cameras.Count == 0)
            {
                _cameras.AddRange(FindObjectsByType<Camera>(FindObjectsSortMode.None));
            }

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

        void Update()
        {
            if (Input.GetKeyDown(_switchKey))
            {
                SwitchCamera();
            }
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
