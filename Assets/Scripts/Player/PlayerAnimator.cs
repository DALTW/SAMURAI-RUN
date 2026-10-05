using UnityEngine;
using SamuraiRunner.Audio;
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

        [Header("발소리")]
        [Tooltip("달리기 클립에서 발이 땅에 닿는 프레임 번호 (RUN 16프레임 기준 5, 13번에서 발이 다시 닿음)")]
        [SerializeField] private int[] footstepFrames = { 5, 13 };

        private float hurtTimer;
        private float baseRunSpeed;
        private int lastRunFrame = -1;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<SpriteSheetAnimator>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (attack == null) attack = GetComponent<PlayerAttack>();
            if (health == null) health = GetComponent<PlayerHealth>();
            baseRunSpeed = motor != null ? motor.RunSpeed : 0f; // 시작 속도 = 달리기 클립 원래 속도
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
            animator.PlaybackSpeed = 1f; // 공격 모션은 공격 판정 시간과 맞춰 원래 속도로
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
                animator.PlaybackSpeed = 1f;
                animator.Play(hurtClip);
                return;
            }

            if (hurtTimer > 0f)
            {
                hurtTimer -= Time.deltaTime;
                animator.PlaybackSpeed = 1f;
                animator.Play(hurtClip);
                return;
            }

            // 공격 중에는 공격 클립을 유지 (공중 베기 포함)
            if (attack != null && attack.IsAttacking)
            {
                animator.PlaybackSpeed = 1f;
                return;
            }

            if (motor == null) return;

            if (!motor.IsGrounded)
            {
                animator.PlaybackSpeed = 1f;
                animator.Play(jumpClip);
            }
            else if (motor.AutoRun)
            {
                // 빨라진 만큼 다리도 빠르게 (미끄러지듯 보이지 않게)
                animator.PlaybackSpeed = baseRunSpeed > 0f ? motor.RunSpeed / baseRunSpeed : 1f;
                animator.Play(runClip);
                PlayFootstepOnContactFrame();
                return;
            }
            else
            {
                animator.PlaybackSpeed = 1f;
                animator.Play(idleClip);
            }
            lastRunFrame = -1; // 달리기가 아닐 때는 발소리 프레임 기억을 지움
        }

        /// <summary>달리기 클립이 발이 땅에 닿는 프레임으로 넘어간 순간 발소리</summary>
        private void PlayFootstepOnContactFrame()
        {
            int frame = animator.FrameIndex;
            if (frame == lastRunFrame) return;
            lastRunFrame = frame;
            if (footstepFrames == null) return;
            foreach (int contact in footstepFrames)
                if (contact == frame) { Sfx.Play(SfxId.Footstep); return; }
        }
    }
}
