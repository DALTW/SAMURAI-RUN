using System;
using UnityEngine;
using SamuraiRunner.Player;

namespace SamuraiRunner.Systems
{
    /// <summary>
    /// 콤보 담당. 기획대로 "저스트 패링"으로만 쌓이고, 피격 시 리셋됩니다.
    /// </summary>
    public class ComboCounter : MonoBehaviour
    {
        [Tooltip("플레이어의 PlayerAttack (씬 자동 구성이 연결)")]
        public PlayerAttack playerAttack;
        [Tooltip("플레이어의 PlayerHealth (씬 자동 구성이 연결)")]
        public PlayerHealth playerHealth;

        public int Combo { get; private set; }
        public int BestCombo { get; private set; }

        public event Action<int> ComboChanged;

        private void OnEnable()
        {
            if (playerAttack != null) playerAttack.ParrySucceeded += HandleParry;
            if (playerHealth != null) playerHealth.Damaged += ResetCombo;
        }

        private void OnDisable()
        {
            if (playerAttack != null) playerAttack.ParrySucceeded -= HandleParry;
            if (playerHealth != null) playerHealth.Damaged -= ResetCombo;
        }

        private void HandleParry(bool isJust, Vector2 position)
        {
            if (!isJust) return; // 일반 패링은 콤보로 인정하지 않음

            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            ComboChanged?.Invoke(Combo);
        }

        private void ResetCombo()
        {
            Combo = 0;
            ComboChanged?.Invoke(Combo);
        }
    }
}
