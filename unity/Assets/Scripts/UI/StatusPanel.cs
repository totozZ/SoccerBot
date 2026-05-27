// StatusPanel.cs — HUD overlay showing robot status and key data.
// Attach to the Canvas GameObject. Uses TextMeshPro (default in Unity 6).

using UnityEngine;
using TMPro;

namespace SoccerBot
{
    public class StatusPanel : MonoBehaviour
    {
        [Header("Text References (drag TMP_Text here)")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _poseText;
        [SerializeField] private TMP_Text _shooterText;
        [SerializeField] private TMP_Text _visionText;

        [Header("Settings")]
        [SerializeField] private float _updateInterval = 0.15f;
        [Tooltip("When false, the entire panel is hidden — keep on for演示 (only Status + Pose are shown if _demoMode is on).")]
        [SerializeField] private bool _showInDemo = true;
        [Tooltip("Demo mode hides the noisy Shooter/Vision lines and keeps only Status + Pose.")]
        [SerializeField] private bool _demoMode = true;

        private float _timer;

        void Start()
        {
            if (!_showInDemo)
            {
                gameObject.SetActive(false);
                return;
            }
            if (_demoMode)
            {
                if (_shooterText != null) _shooterText.gameObject.SetActive(false);
                if (_visionText  != null) _visionText.gameObject.SetActive(false);
            }
            UpdateUI();
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _updateInterval)
            {
                _timer = 0f;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (GameManager.Instance == null) return;
            var gm = GameManager.Instance;

            SetText(_statusText, gm.GetStatusString());

            var pos = gm.Robot.position;
            var rot = gm.Robot.rotation.eulerAngles.y;
            SetText(_poseText, $"Pose: X={pos.x:F2}  Z={pos.z:F2}  Yaw={rot:F1}°");

            if (_demoMode) return;

            var s = gm.Shooter;
            SetText(_shooterText,
                $"Shooter: {s.speed:F0} RPM | Hood: {s.angle:F1}° | " +
                $"Loaded: {s.isLoaded} | Firing: {s.isFiring}");

            var v = gm.Vision;
            SetText(_visionText, v.hasTarget
                ? $"Target: tx={v.targetX:F1}°  ty={v.targetY:F1}°  dist={v.distance:F2}m"
                : "Target: none");
        }

        private void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
