using System;
using UnityEngine;
using SamuraiRunner.Audio;

namespace SamuraiRunner.Player
{
    /// <summary>
    /// 플레이어의 "체력"만 담당: 3심장제, 피격 시 무적 시간 + 깜빡임, 회복, 낙사, 사망 이벤트.
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

        [Header("낙사")]
        [Tooltip("플레이어 위치(월드 Y)가 이보다 낮아지면 체력과 상관없이 즉사합니다 (구덩이). " +
                 "서 있을 때 Y는 약 -0.42, 화면 아래 끝은 약 -2.6 — 몸이 화면 밖으로 사라진 뒤 판정되게 둡니다")]
        [SerializeField] private float fallDeathY = -3f;

        public int MaxHearts => maxHearts;
        public int CurrentHearts { get; private set; }
        public bool IsDead => CurrentHearts <= 0;
        public bool IsFull => CurrentHearts >= maxHearts;
        public bool IsInvulnerable => invulnTimer > 0f;
        /// <summary>구덩이에 떨어져 죽었는지</summary>
        public bool DiedFromFall { get; private set; }

        /// <summary>(현재 심장, 최대 심장) — HUD가 구독합니다.</summary>
        public event Action<int, int> HealthChanged;
        /// <summary>피해를 입은 순간 (애니메이션·콤보 리셋용)</summary>
        public event Action Damaged;
        /// <summary>심장을 다 잃은 순간 (GameFlowController가 구독)</summary>
        public event Action Died;
        /// <summary>회복한 순간 (회복 아이템 연출 등에 사용)</summary>
        public event Action Healed;

        private float invulnTimer;
        private float blinkTimer;
        private Rigidbody2D body;

        private void Awake()
        {
            CurrentHearts = maxHearts;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            UpdateFall();

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
                Sfx.Play(SfxId.Death);
                Died?.Invoke();
            }
            else
            {
                Sfx.Play(SfxId.Hurt);
                invulnTimer = invulnerableTime;
                blinkTimer = 0f;
            }
            return true;
        }

        /// <summary>체력·무적과 상관없이 즉시 사망합니다 (낙사 등). 이미 죽었으면 아무 일도 없습니다.</summary>
        public void Kill()
        {
            if (IsDead) return;

            CurrentHearts = 0;
            invulnTimer = 0f;
            if (spriteRenderer != null) spriteRenderer.enabled = true;

            HealthChanged?.Invoke(CurrentHearts, maxHearts);
            Damaged?.Invoke();
            Sfx.Play(DiedFromFall ? SfxId.Fall : SfxId.Death); // 구덩이면 떨어지는 휘파람, 아니면 쓰러지는 소리
            Died?.Invoke();
        }

        private void UpdateFall()
        {
            if (!IsDead && transform.position.y < fallDeathY)
            {
                DiedFromFall = true;
                Kill();
            }

            // 죽은 채로 화면 아래로 충분히 사라지면(공중에서 맞고 구덩이로 떨어진 경우 포함) 물리를 멈춰 끝없이 떨어지지 않게
            if (IsDead && body != null && body.simulated && transform.position.y < fallDeathY - 8f)
                body.simulated = false;
        }

        /// <summary>심장을 회복합니다. 사망했거나 이미 가득 차 있으면 false를 반환하고 아무 일도 일어나지 않습니다.</summary>
        public bool Heal(int amount = 1)
        {
            if (IsDead || IsFull || amount <= 0) return false;

            CurrentHearts = Mathf.Min(maxHearts, CurrentHearts + amount);
            HealthChanged?.Invoke(CurrentHearts, maxHearts);
            Sfx.Play(SfxId.Heal);
            Healed?.Invoke();
            return true;
        }
    }
}
