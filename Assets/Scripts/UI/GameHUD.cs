using UnityEngine;
using UnityEngine.UI;
using SamuraiRunner.Player;
using SamuraiRunner.Systems;

namespace SamuraiRunner.UI
{
    /// <summary>
    /// 화면 표시 담당: 심장(체력), 콤보, 달린 거리.
    /// UI 요소들은 씬 자동 구성이 만들며, 위치·크기는 Scene에서 수정하면 됩니다.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("연결 (씬 자동 구성이 채움)")]
        public PlayerHealth playerHealth;
        public ComboCounter comboCounter;
        public DistanceTracker distanceTracker;

        [Header("UI 요소")]
        public Image[] heartIcons;
        public Text comboText;
        public Text distanceText;

        [Header("색")]
        [SerializeField] private Color heartFullColor = new Color(0.88f, 0.22f, 0.28f);
        [SerializeField] private Color heartEmptyColor = new Color(0.25f, 0.2f, 0.2f, 0.6f);

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.HealthChanged += HandleHealthChanged;
            if (comboCounter != null) comboCounter.ComboChanged += HandleComboChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.HealthChanged -= HandleHealthChanged;
            if (comboCounter != null) comboCounter.ComboChanged -= HandleComboChanged;
        }

        private void Start()
        {
            if (playerHealth != null)
                HandleHealthChanged(playerHealth.CurrentHearts, playerHealth.MaxHearts);
            HandleComboChanged(comboCounter != null ? comboCounter.Combo : 0);
        }

        private void Update()
        {
            if (distanceText != null && distanceTracker != null)
                distanceText.text = $"{distanceTracker.DistanceMeters:0} m";
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (heartIcons == null) return;
            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] == null) continue;
                heartIcons[i].color = i < current ? heartFullColor : heartEmptyColor;
            }
        }

        private void HandleComboChanged(int combo)
        {
            if (comboText != null)
                comboText.text = combo > 0 ? $"{combo} COMBO" : string.Empty;
        }
    }
}
