using UnityEngine;
using SamuraiRunner.Common;

namespace SamuraiRunner.Player
{
    /// <summary>
    /// 플레이어 상태(사망 > 피격 > 공격 > 공중 > 달리기 > 대기)를 보고 알맞은 클립을 재생하는
    /// "애니메이션 선택"만 담당. 클립 이름은 Scene의 Inspector에서 바꿀 수 있습니다.
    /// </summary>
    [RequireComponent(typeof(SpriteSheetAnimator))]
    public class PlayerAnimator : MonoBehaviour
    {
        [Header("연결 (비워두면 자동으로 찾습니다)")]
        [SerializeField] private SpriteSheetAnimator animator;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerAttack attack;
        [SerializeField] private PlayerHealth health;

        [Header("클립 이름 (SpriteSheetAnimator의 클립 이름과 일치)")]
        [SerializeField] private string idleClip = "IDLE";
        [SerializeField] private string runClip = "RUN";
        [SerializeField] private string jumpClip = "JUMP";
        [SerializeField] private string attackClip = "ATTACK";
        [SerializeField] private string hurtClip = "HURT";

        [Header("피격 연출")]
        [Tooltip("피격 시 HURT 클립을 유지하는 시간(초)")]
        [SerializeField] private float hurtAnimTime = 0.35f;

        private float hurtTimer;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<SpriteSheetAnimator>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (attack == null) attack = GetComponent<PlayerAttack>();
            if (health == null) health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (attack != null) attack.AttackStarted += HandleAttackStarted;
            if (health != null) health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (attack != null) attack.AttackStarted -= HandleAttackStarted;
            if (health != null) health.Damaged -= HandleDamaged;
        }

        private void HandleAttackStarted()
        {
            animator.Play(attackClip, restart: true);
        }

        private void HandleDamaged()
        {
            hurtTimer = hurtAnimTime;
            animator.Play(hurtClip, restart: true);
        }

        private void LateUpdate()
        {
            // 사망: HURT 클립 마지막 프레임에서 정지 (페이드아웃은 GameFlowController가 담당)
            if (health != null && health.IsDead)
            {
                animator.Play(hurtClip);
                return;
            }

            if (hurtTimer > 0f)
            {
                hurtTimer -= Time.deltaTime;
                animator.Play(hurtClip);
                return;
            }

            // 공격 중에는 공격 클립을 유지 (공중 베기 포함)
            if (attack != null && attack.IsAttacking)
                return;

            if (motor == null) return;

            if (!motor.IsGrounded)
                animator.Play(jumpClip);
            else if (motor.AutoRun)
                animator.Play(runClip);
            else
                animator.Play(idleClip);
        }
    }
}
