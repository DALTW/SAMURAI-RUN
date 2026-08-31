using UnityEngine;
using SamuraiRunner.Combat;
using SamuraiRunner.Effects;
using SamuraiRunner.Player;

namespace SamuraiRunner.Enemies
{
    /// <summary>
    /// 서서 투사체를 던지는 적 (궁수·수리검 닌자 공용).
    /// 어떤 투사체를 얼마나 자주 던질지는 프리팹/Scene에서 조절합니다.
    /// 저스트 패링으로 반사된 투사체에 맞으면 죽습니다 (Projectile이 Kill 호출).
    /// </summary>
    public class ThrowerEnemy : MonoBehaviour
    {
        [Header("발사")]
        [Tooltip("던질 투사체 프리팹")]
        public Projectile projectilePrefab;
        [SerializeField] private float fireInterval = 1.6f;
        [Tooltip("플레이어를 발견한 뒤 첫 발까지의 지연(초)")]
        [SerializeField] private float firstShotDelay = 0.35f;
        [Tooltip("플레이어가 이 거리(유닛) 안에 들어오면 발사 시작")]
        [SerializeField] private float detectRange = 13f;
        [Tooltip("투사체가 나가는 위치 (적 기준 오프셋)")]
        [SerializeField] private Vector2 firePointOffset = new Vector2(-0.45f, 0.15f);
        [Tooltip("체크하면 플레이어의 가슴 높이를 조준, 아니면 왼쪽으로 직사")]
        [SerializeField] private bool aimAtPlayer = false;
        [Tooltip("조준 시 플레이어 위치 보정 (가슴 높이)")]
        [SerializeField] private Vector2 aimOffset = new Vector2(0f, -0.5f);

        [Header("죽음")]
        [Tooltip("죽을 때 나올 이펙트 프리팹")]
        public FlashEffect deathEffectPrefab;

        private Transform player;
        private float timer;
        private bool seen;

        private void Start()
        {
            var health = Object.FindFirstObjectByType<PlayerHealth>();
            if (health != null) player = health.transform;
            timer = firstShotDelay;
        }

        private void Update()
        {
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
                Fire();
            }
        }

        private void Fire()
        {
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
            if (deathEffectPrefab != null)
            {
                var fx = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
                fx.Init(new Color(1f, 0.5f, 0.3f), 1.4f);
            }
            Destroy(gameObject);
        }
    }
}
