// StatusPanel.cs — HUD panel showing robot connection status and key data.
// Attach to a Canvas GameObject. Requires TextMeshPro or legacy Text.

using UnityEngine;
using UnityEngine.UI;

namespace SoccerBot
{
    public class StatusPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _poseText;
        [SerializeField] private Text _shooterText;
        [SerializeField] private Text _visionText;

        [Header("Update Interval")]
        [SerializeField] private float _updateInterval = 0.2f;

        private float _timer;

        void Start()
        {
            if (GameManager.Instance != null)
            {
                UpdateUI();
            }
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

            // Status line
            if (_statusText != null)
            {
                _statusText.text = gm.GetStatusString();
            }

            // Robot pose
            if (_poseText != null)
            {
                var pos = gm.Robot.position;
                var rot = gm.Robot.rotation.eulerAngles.y;
                _poseText.text = $"Pose: X={pos.x:F2}  Z={pos.z:F2}  θ={rot:F1}°";
            }

            // Shooter
            if (_shooterText != null)
            {
                var s = gm.Shooter;
                _shooterText.text =
                    $"Shooter: {s.speed:F0} RPM | Hood: {s.angle:F1}° | " +
                    $"Loaded: {s.isLoaded} | Firing: {s.isFiring}";
            }

            // Vision
            if (_visionText != null)
            {
                var v = gm.Vision;
                _visionText.text = v.hasTarget
                    ? $"Target: tx={v.targetX:F1}° ty={v.targetY:F1}° dist={v.distance:F2}m"
                    : "Target: none";
            }
        }
    }
}
