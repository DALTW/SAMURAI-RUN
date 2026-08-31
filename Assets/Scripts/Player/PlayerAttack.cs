using System;
using System.Collections.Generic;
using UnityEngine;
using SamuraiRunner.Common;

namespace SamuraiRunner.Player
{
    /// <summary>
    /// 플레이어의 "베기"만 담당: 공격 타이밍, 칼 판정(히트박스), 패링/저스트 패링 구분.
    /// 버튼을 누른 뒤 justParryWindow 안에 투사체를 베면 저스트 패링(반사),
    /// 그 이후에 베면 일반 패링(쳐내기)입니다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerAttack : MonoBehaviour
    {
        [Header("공격 타이밍")]
        [Tooltip("공격 모션 전체 길이(초) — ATTACK 클립 길이와 맞추면 자연스럽습니다")]
        [SerializeField] private float attackDuration = 0.4f;
        [Tooltip("공격이 끝난 뒤 다음 공격까지의 대기 시간(초)")]
        [SerializeField] private float attackCooldown = 0.05f;

        [Header("패링 판정")]
        [Tooltip("버튼 입력 후 이 시간(초) 안에 맞히면 저스트 패링 — 6~8프레임 = 0.10~0.13초")]
        [SerializeField] private float justParryWindow = 0.15f;

        [Header("칼 판정 (히트박스)")]
        [Tooltip("공격 시작 후 칼 판정이 켜지는 시각(초)")]
        [SerializeField] private float hitActiveFrom = 0.04f;
        [Tooltip("공격 시작 후 칼 판정이 꺼지는 시각(초)")]
        [SerializeField] private float hitActiveUntil = 0.3f;
        [Tooltip("히트박스 위치 (플레이어 기준 오프셋)")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.9f, -0.5f);
        [Tooltip("히트박스 크기")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.3f, 1.0f);
        [Tooltip("칼에 맞을 수 있는 레이어 (투사체 레이어)")]
        [SerializeField] private LayerMask hittableLayers;

        public bool IsAttacking { get; private set; }

        /// <summary>공격이 시작되는 순간 호출됩니다 (애니메이션 재시작용).</summary>
        public event Action AttackStarted;

        /// <summary>패링 성공 시 호출: (저스트 여부, 맞힌 위치). 히트스톱·콤보·이펙트가 구독합니다.</summary>
        public event Action<bool, Vector2> ParrySucceeded;

        private PlayerInputReader input;
        private float attackTimer;
        private float cooldownTimer;
        private readonly HashSet<Collider2D> hitThisSwing = new HashSet<Collider2D>();
        private static readonly List<Collider2D> overlapResults = new List<Collider2D>(8);

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            cooldownTimer -= Time.deltaTime;

            if (IsAttacking)
            {
                attackTimer += Time.deltaTime;

                if (attackTimer >= hitActiveFrom && attackTimer <= hitActiveUntil)
                    DoHitCheck();

                if (attackTimer >= attackDuration)
                {
                    IsAttacking = false;
                    cooldownTimer = attackCooldown;
                }
            }
            else if (cooldownTimer <= 0f && input.AttackPressed)
            {
                IsAttacking = true;
                attackTimer = 0f;
                hitThisSwing.Clear();
                AttackStarted?.Invoke();
            }
        }

        private void DoHitCheck()
        {
            Vector2 center = (Vector2)transform.position + hitboxOffset;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hittableLayers, useTriggers = true };
            int count = Physics2D.OverlapBox(center, hitboxSize, 0f, filter, overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit == null || hitThisSwing.Contains(hit)) continue;
                hitThisSwing.Add(hit);

                if (hit.TryGetComponent<IParryable>(out var parryable))
                {
                    bool isJust = attackTimer <= justParryWindow;
                    parryable.OnParried(gameObject, isJust);
                    ParrySucceeded?.Invoke(isJust, hit.transform.position);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)transform.position + hitboxOffset, hitboxSize);
        }
    }
}
