using UnityEngine;
using SamuraiRunner.Player;

namespace SamuraiRunner.Items
{
    /// <summary>
    /// 체력 회복 아이템 (주먹밥).
    /// 생성되면 살짝 튀어올랐다가 바닥에 떨어져 둥실거리고, 플레이어가 닿으면 심장을 회복한 뒤 사라집니다.
    /// 체력이 가득 차 있으면 기본적으로 먹지 않고 그대로 남습니다 (플레이어는 그냥 지나감).
    /// 떨어지는 높이·회복량·둥실거림은 프리팹에서 조절합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HealthPickup : MonoBehaviour
    {
        [Header("회복")]
        [Tooltip("회복할 심장 개수")]
        [SerializeField] private int healAmount = 1;
        [Tooltip("체크하면 체력이 가득 차 있어도 먹고 사라집니다 (회복은 없음)")]
        [SerializeField] private bool collectWhenFull = false;

        [Header("튀어오르기 (드롭될 때)")]
        [Tooltip("드롭 순간의 속도 (x: 앞쪽, y: 위쪽)")]
        [SerializeField] private Vector2 popVelocity = new Vector2(0.5f, 4.5f);
        [Tooltip("떨어지는 중력 (유닛/초²)")]
        [SerializeField] private float gravity = 18f;
        [Tooltip("바닥으로 인식할 레이어 (아래로 레이를 쏴서 착지 높이를 찾음)")]
        [SerializeField] private LayerMask groundLayer;

        [Header("둥실거림 (착지 후)")]
        [SerializeField] private float bobAmplitude = 0.06f;
        [SerializeField] private float bobSpeed = 4f;

        [Header("먹었을 때 연출")]
        [Tooltip("먹은 뒤 위로 떠오르며 사라지는 시간(초)")]
        [SerializeField] private float collectFadeTime = 0.25f;
        [Tooltip("먹은 뒤 떠오르는 높이 (유닛)")]
        [SerializeField] private float collectRise = 0.5f;

        private SpriteRenderer sr;
        private Collider2D col;
        private Vector2 velocity;
        private bool airborne;
        private float landY;
        private float bobTime;
        private bool collected;
        private float collectTimer;
        private Vector3 collectStart;
        private Vector3 baseScale;

        private void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            baseScale = transform.localScale;
            landY = transform.position.y;
        }

        /// <summary>
        /// 드롭 연출을 시작합니다. fallbackGroundY는 바닥을 레이로 찾지 못했을 때 쓸 바닥 높이(발 기준)입니다.
        /// </summary>
        public void Launch(float fallbackGroundY)
        {
            float groundY = fallbackGroundY;
            if (groundLayer.value != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 20f, groundLayer);
                if (hit.collider != null) groundY = hit.point.y;
            }

            // 스프라이트 아래끝이 바닥에 닿는 높이
            float bottomOffset = sr != null && sr.sprite != null ? -sr.sprite.bounds.min.y * transform.localScale.y : 0f;
            landY = groundY + bottomOffset;

            velocity = popVelocity;
            airborne = true;
            bobTime = 0f;
        }

        private void Update()
        {
            if (collected)
            {
                UpdateCollect();
                return;
            }

            if (airborne)
            {
                velocity.y -= gravity * Time.deltaTime;
                Vector3 p = transform.position + (Vector3)(velocity * Time.deltaTime);
                if (velocity.y < 0f && p.y <= landY)
                {
                    p.y = landY;
                    airborne = false;
                }
                transform.position = p;
                return;
            }

            // 바닥 위에서 둥실둥실 (가장 낮을 때가 바닥에 닿는 높이)
            bobTime += Time.deltaTime;
            Vector3 pos = transform.position;
            pos.y = landY + bobAmplitude * (1f + Mathf.Sin(bobTime * bobSpeed - Mathf.PI * 0.5f));
            transform.position = pos;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        // 체력이 가득 찬 채로 겹쳐 있다가 그 사이에 맞아서 체력이 줄면 바로 먹을 수 있게
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (collected) return;
            if (!other.TryGetComponent<PlayerHealth>(out var health)) return;
            if (health.IsDead) return;

            bool healed = health.Heal(healAmount);
            if (!healed && !collectWhenFull) return;

            collected = true;
            if (col != null) col.enabled = false;
            collectTimer = 0f;
            collectStart = transform.position;
        }

        private void UpdateCollect()
        {
            collectTimer += Time.unscaledDeltaTime; // 히트스톱 중에도 연출 진행
            float k = collectFadeTime > 0f ? Mathf.Clamp01(collectTimer / collectFadeTime) : 1f;

            transform.position = collectStart + Vector3.up * (collectRise * k);
            transform.localScale = baseScale * Mathf.Lerp(1f, 1.3f, k);
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f - k;
                sr.color = c;
            }

            if (k >= 1f) Destroy(gameObject);
        }
    }
}
