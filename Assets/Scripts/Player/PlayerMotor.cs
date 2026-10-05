using UnityEngine;
using SamuraiRunner.Audio;

namespace SamuraiRunner.Player
{
    /// <summary>
    /// 플레이어의 "이동"만 담당: 자동 달리기, 점프, 중력, 접지 판정.
    /// 모든 수치는 Scene의 Inspector에서 조정할 수 있습니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("달리기")]
        [Tooltip("체크하면 러너처럼 오른쪽으로 자동으로 계속 달립니다")]
        [SerializeField] private bool autoRun = true;
        [Tooltip("달리기 속도 (유닛/초)")]
        [SerializeField] private float runSpeed = 5f;

        [Header("점프")]
        [Tooltip("점프 순간의 상승 속도")]
        [SerializeField] private float jumpForce = 13f;
        [Tooltip("점프 키를 일찍 떼면 상승 속도에 이 값을 곱해 낮은 점프가 됩니다 (0.4~0.6 추천)")]
        [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.45f;
        [Tooltip("발판에서 떨어진 직후에도 점프를 허용하는 시간(초) — 코요테 타임")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("착지 직전에 미리 누른 점프를 기억해 주는 시간(초) — 점프 버퍼")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("중력")]
        [Tooltip("기본 중력 배율 (Rigidbody2D의 Gravity Scale)")]
        [SerializeField] private float gravityScale = 3.5f;
        [Tooltip("낙하 중에는 중력에 이 값을 곱해 더 빠르게 떨어집니다")]
        [SerializeField] private float fallGravityMultiplier = 1.5f;

        [Header("접지 판정")]
        [Tooltip("바닥으로 인식할 레이어")]
        [SerializeField] private LayerMask groundLayer;
        [Tooltip("발 밑 판정 박스의 위치 (플레이어 기준 오프셋)")]
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -1.05f);
        [Tooltip("발 밑 판정 박스의 크기")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.12f);

        public bool IsGrounded { get; private set; }
        public bool AutoRun { get => autoRun; set => autoRun = value; }
        /// <summary>현재 달리기 속도 (유닛/초). SpeedRamp가 달린 거리에 따라 올립니다.</summary>
        public float RunSpeed { get => runSpeed; set => runSpeed = Mathf.Max(0f, value); }
        public float VerticalVelocity => body.linearVelocity.y;

        /// <summary>사망 등으로 달리기를 완전히 멈춥니다 (GameFlowController가 호출).</summary>
        public void StopRunning()
        {
            autoRun = false;
            if (body != null)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        private Rigidbody2D body;
        private PlayerInputReader input;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private float airTime;
        private const float MinAirTimeForLandSound = 0.12f; // 아주 짧게 뜬 건 착지 소리 없이

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();
            body.freezeRotation = true;
            body.gravityScale = gravityScale;
        }

        private void Update()
        {
            // 점프 입력을 버퍼에 저장 (프레임 단위 입력은 Update에서만 정확함)
            if (input.JumpPressed)
                jumpBufferTimer = jumpBufferTime;
            else
                jumpBufferTimer -= Time.deltaTime;

            // 상승 중에 키를 떼면 점프를 짧게 끊기 (가변 점프 높이)
            if (input.JumpReleased && body.linearVelocity.y > 0f)
                body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpCutMultiplier);
        }

        private void FixedUpdate()
        {
            bool wasGrounded = IsGrounded;
            CheckGround();

            // 착지 소리: 잠깐이라도 공중에 떠 있다가 발이 닿은 순간
            if (IsGrounded)
            {
                if (!wasGrounded && airTime > MinAirTimeForLandSound) Sfx.Play(SfxId.Land);
                airTime = 0f;
            }
            else airTime += Time.fixedDeltaTime;

            coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - Time.fixedDeltaTime;

            if (autoRun)
                body.linearVelocity = new Vector2(runSpeed, body.linearVelocity.y);

            // 버퍼된 점프 + 코요테 타임이 겹치면 점프 실행
            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
                Sfx.Play(SfxId.Jump);
            }

            // 낙하 중에는 중력 강화 → 점프가 무겁고 경쾌한 느낌
            body.gravityScale = body.linearVelocity.y < 0f ? gravityScale * fallGravityMultiplier : gravityScale;
        }

        private void CheckGround()
        {
            Vector2 center = (Vector2)transform.position + groundCheckOffset;
            IsGrounded = Physics2D.OverlapBox(center, groundCheckSize, 0f, groundLayer) != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube((Vector2)transform.position + groundCheckOffset, groundCheckSize);
        }
    }
}
