using System;
using UnityEngine;

namespace SamuraiRunner.Player
{
    /// <summary>
    /// 플레이어의 "체력"만 담당: 3심장제, 피격 시 무적 시간 + 깜빡임, 사망 이벤트.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("체력")]
        [Tooltip("최대 심장 개수")]
        [SerializeField] private int maxHearts = 3;

        [Header("피격")]
        [Tooltip("피격 후 무적 시간(초)")]
        [SerializeField] private float invulnerableTime = 1.2f;
        [Tooltip("무적 중 깜빡임 간격(초)")]
        [SerializeField] private float blinkInterval = 0.1f;
        [Tooltip("비워두면 같은 오브젝트의 SpriteRenderer를 자동으로 사용")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        public int MaxHearts => maxHearts;
        public int CurrentHearts { get; private set; }
        public bool IsDead => CurrentHearts <= 0;
        public bool IsInvulnerable => invulnTimer > 0f;

        /// <summary>(현재 심장, 최대 심장) — HUD가 구독합니다.</summary>
        public event Action<int, int> HealthChanged;
        /// <summary>피해를 입은 순간 (애니메이션·콤보 리셋용)</summary>
        public event Action Damaged;
        /// <summary>심장을 다 잃은 순간 (GameFlowController가 구독)</summary>
        public event Action Died;

        private float invulnTimer;
        private float blinkTimer;

        private void Awake()
        {
            CurrentHearts = maxHearts;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (invulnTimer <= 0f) return;

            invulnTimer -= Time.deltaTime;
            blinkTimer -= Time.deltaTime;

            if (spriteRenderer != null && blinkTimer <= 0f)
            {
                blinkTimer = blinkInterval;
                spriteRenderer.enabled = !spriteRenderer.enabled;
            }

            if (invulnTimer <= 0f && spriteRenderer != null)
                spriteRenderer.enabled = true;
        }

        /// <summary>피해를 시도합니다. 무적/사망 상태면 false를 반환하고 아무 일도 일어나지 않습니다.</summary>
        public bool TakeDamage(int amount = 1)
        {
            if (IsDead || IsInvulnerable) return false;

            CurrentHearts = Mathf.Max(0, CurrentHearts - amount);
            HealthChanged?.Invoke(CurrentHearts, maxHearts);
            Damaged?.Invoke();

            if (IsDead)
            {
                if (spriteRenderer != null) spriteRenderer.enabled = true;
                Died?.Invoke();
            }
            else
            {
                invulnTimer = invulnerableTime;
                blinkTimer = 0f;
            }
            return true;
        }
    }
}
