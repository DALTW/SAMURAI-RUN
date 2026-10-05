using UnityEngine;
using SamuraiRunner.Combat;
using SamuraiRunner.Common;
using SamuraiRunner.Effects;
using SamuraiRunner.Player;
using SamuraiRunner.Audio;

namespace SamuraiRunner.Enemies
{
    /// <summary>
    /// 서서 투사체를 던지는 적 (궁수·수리검 닌자 공용).
    /// 공격 흐름: 대기(IDLE) → 예비 동작(ATTACK 클립 시작) → 발사 프레임에서 투사체 생성 → 클립이 끝나면 대기 복귀.
    /// 예비 동작이 보이기 때문에 플레이어가 타이밍을 읽고 패링을 준비할 수 있습니다.
    /// SpriteSheetAnimator나 ATTACK 클립이 없으면 예전처럼 즉시 발사합니다.
    /// 어떤 투사체를 얼마나 자주 던질지는 프리팹/Scene에서 조절합니다.
    /// 저스트 패링으로 반사된 투사체에 맞으면 죽습니다 (Projectile이 Kill 호출).
    /// </summary>
    public class ThrowerEnemy : MonoBehaviour
    {
        [Header("발사")]
        [Tooltip("던질 투사체 프리팹")]
        public Projectile projectilePrefab;
        [Tooltip("공격 시작 간격(초) — 예비 동작 시작 시점 기준")]
        [SerializeField] private float fireInterval = 1.6f;
        [Tooltip("플레이어를 발견한 뒤 첫 공격까지의 지연(초)")]
        [SerializeField] private float firstShotDelay = 0.35f;
        [Tooltip("플레이어가 이 거리(유닛) 안에 들어오면 공격 시작")]
        [SerializeField] private float detectRange = 13f;
        [Tooltip("투사체가 나가는 위치 (적 기준 오프셋) — 손·활 끝에 맞추세요")]
        [SerializeField] private Vector2 firePointOffset = new Vector2(-0.45f, 0.15f);
        [Tooltip("체크하면 플레이어의 가슴 높이를 조준, 아니면 왼쪽으로 직사")]
        [SerializeField] private bool aimAtPlayer = false;
        [Tooltip("조준 시 플레이어 위치 보정 (가슴 높이)")]
        [SerializeField] private Vector2 aimOffset = new Vector2(0f, -0.5f);

        [Header("애니메이션 (비워두면 같은 오브젝트에서 찾음)")]
        [SerializeField] private SpriteSheetAnimator animator;
        [Tooltip("대기 클립 이름 (반복)")]
        [SerializeField] private string idleClip = "IDLE";
        [Tooltip("공격 클립 이름 (반복 없음) — 활 당기기/던지기 예비 동작 포함")]
        [SerializeField] private string attackClip = "ATTACK";
        [Tooltip("ATTACK 클립에서 투사체가 실제로 나가는 프레임 번호 (0부터)")]
        [SerializeField] private int releaseFrame = 3;
        [Tooltip("죽음 클립 이름 (반복 없음). 없으면 이펙트만 내고 바로 사라집니다")]
        [SerializeField] private string deathClip = "DEATH";

        [Header("죽음")]
        [Tooltip("죽을 때 나올 이펙트 프리팹")]
        public FlashEffect deathEffectPrefab;

        public bool IsAttacking { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>처치된 순간 한 번 호출됩니다 (아이템 드롭 등이 구독).</summary>
        public event System.Action Killed;

        private Transform player;
        private float timer;
        private bool seen;
        private bool released;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<SpriteSheetAnimator>();
        }

        private void Start()
        {
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            if (health != null) player = health.transform;
            timer = firstShotDelay;

            if (animator != null && animator.HasClip(idleClip))
                animator.Play(idleClip);
        }

        private void Update()
        {
            if (IsDead) return;

            // 공격 중: 발사 프레임에서 투사체를 내고, 클립이 끝나면 대기로 돌아감
            if (IsAttacking)
            {
                UpdateAttack();
                return;
            }

            if (player == null || projectilePrefab == null) return;

            float dx = transform.position.x - player.position.x;
            if (dx < 0f) return;              // 플레이어가 이미 지나감
            if (dx > detectRange) return;     // 아직 너무 멀다

            if (!seen)
            {
                seen = true;
                timer = firstShotDelay;
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = fireInterval;
                BeginAttack();
            }
        }

        private void BeginAttack()
        {
            if (animator == null || !animator.HasClip(attackClip))
            {
                Fire(); // 애니메이션이 없으면 즉시 발사 (예전 동작)
                return;
            }

            IsAttacking = true;
            released = false;
            animator.Play(attackClip, restart: true);

            // 발사 프레임이 0이면 시작과 동시에 발사
            if (animator.FrameIndex >= releaseFrame) Release();
        }

        private void UpdateAttack()
        {
            // 다른 클립으로 바뀌었다면(외부 개입) 공격 상태를 정리
            if (animator == null || animator.CurrentClipName != attackClip)
            {
                EndAttack();
                return;
            }

            if (!released && animator.FrameIndex >= releaseFrame)
                Release();

            if (animator.IsFinished)
                EndAttack();
        }

        private void Release()
        {
            released = true;
            Fire();
        }

        private void EndAttack()
        {
            if (!released) Release(); // 클립이 발사 프레임보다 짧아도 한 발은 나가게
            IsAttacking = false;
            if (animator != null && animator.HasClip(idleClip))
                animator.Play(idleClip);
        }

        private void Fire()
        {
            if (projectilePrefab == null) return;

            Vector2 origin = (Vector2)transform.position + firePointOffset;
            Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);

            Vector2 dir = aimAtPlayer && player != null
                ? ((Vector2)player.position + aimOffset - origin).normalized
                : Vector2.left;

            projectile.Launch(dir, gameObject);
        }

        /// <summary>반사된 투사체가 명중했을 때 호출됩니다.</summary>
        public void Kill()
        {
            if (IsDead) return;
            IsDead = true;
            IsAttacking = false;

            if (deathEffectPrefab != null)
            {
                var fx = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
                fx.Init(new Color(1f, 0.5f, 0.3f), 1.4f);
            }

            Killed?.Invoke();
            Sfx.Play(SfxId.EnemyDeath);

            // 죽음 클립이 있으면 몸통 판정을 끄고 끝까지 재생한 뒤 제거
            if (animator != null && animator.HasClip(deathClip))
            {
                foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
                if (TryGetComponent<DamageOnTouch>(out var touch)) touch.enabled = false;

                animator.ClipFinished += HandleDeathClipFinished;
                animator.Play(deathClip, restart: true);
                Destroy(gameObject, 3f); // 클립이 어떤 이유로 끝나지 않아도 정리
                return;
            }

            Destroy(gameObject);
        }

        private void HandleDeathClipFinished(string clipName)
        {
            if (clipName != deathClip) return;
            animator.ClipFinished -= HandleDeathClipFinished;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (animator != null) animator.ClipFinished -= HandleDeathClipFinished;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere((Vector2)transform.position + firePointOffset, 0.08f);
        }
    }
}
