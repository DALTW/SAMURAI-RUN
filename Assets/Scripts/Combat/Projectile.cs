using UnityEngine;
using SamuraiRunner.Common;
using SamuraiRunner.Enemies;
using SamuraiRunner.Player;

namespace SamuraiRunner.Combat
{
    /// <summary>
    /// 화살·수리검 등 투사체. 하나의 스크립트로 종류별 차이는 Scene/프리팹 수치로만 조절합니다.
    /// - 일반 패링: 위로 튕겨나가며 무력화
    /// - 저스트 패링: 발사한 적에게 반사되어 적을 처치
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IParryable
    {
        public enum State { Flying, Deflected, Reflected }

        [Header("비행")]
        [Tooltip("비행 속도 (유닛/초)")]
        [SerializeField] private float speed = 7f;
        [Tooltip("체크하면 스프라이트가 진행 방향을 바라봅니다 (화살용)")]
        [SerializeField] private bool rotateTowardsDirection = true;
        [Tooltip("0보다 크면 계속 회전합니다 (수리검용, 도/초)")]
        [SerializeField] private float spinSpeed = 0f;
        [Tooltip("이 시간(초)이 지나면 자동 제거")]
        [SerializeField] private float lifeTime = 10f;

        [Header("패링 반응")]
        [Tooltip("저스트 패링으로 반사될 때 속도 배율")]
        [SerializeField] private float reflectSpeedMultiplier = 1.6f;
        [Tooltip("일반 패링으로 튕겨나가는 방향")]
        [SerializeField] private Vector2 deflectDirection = new Vector2(0.7f, 1f);
        [SerializeField] private Color deflectedColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        [SerializeField] private Color reflectedColor = new Color(1f, 0.9f, 0.4f);

        [Header("피해")]
        [SerializeField] private int damage = 1;

        public State CurrentState { get; private set; } = State.Flying;

        private Vector2 direction = Vector2.left;
        private GameObject owner;
        private SpriteRenderer sr;
        private float age;

        /// <summary>발사 시 방향과 발사자를 지정합니다 (ThrowerEnemy가 호출).</summary>
        public void Launch(Vector2 dir, GameObject shooter)
        {
            direction = dir.normalized;
            owner = shooter;
            ApplyRotation();
        }

        private void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(direction * (speed * Time.deltaTime));

            if (spinSpeed > 0f)
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }

        private void ApplyRotation()
        {
            if (rotateTowardsDirection && spinSpeed <= 0f)
                transform.right = direction;
        }

        public void OnParried(GameObject attacker, bool isJustParry)
        {
            if (CurrentState != State.Flying) return;

            if (isJustParry)
            {
                // 저스트 패링: 발사한 적에게 그대로 반사
                CurrentState = State.Reflected;
                direction = owner != null
                    ? ((Vector2)(owner.transform.position - transform.position)).normalized
                    : -direction;
                speed *= reflectSpeedMultiplier;
                if (sr != null) sr.color = reflectedColor;
                ApplyRotation();
            }
            else
            {
                // 일반 패링: 위로 쳐내며 무력화
                CurrentState = State.Deflected;
                direction = deflectDirection.normalized;
                rotateTowardsDirection = false;
                spinSpeed = 720f;
                if (sr != null) sr.color = deflectedColor;
                Destroy(gameObject, 0.7f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (CurrentState == State.Flying && other.TryGetComponent<PlayerHealth>(out var health))
            {
                if (health.TakeDamage(damage))
                    Destroy(gameObject);
            }
            else if (CurrentState == State.Reflected && other.TryGetComponent<ThrowerEnemy>(out var enemy))
            {
                enemy.Kill();
                Destroy(gameObject);
            }
        }
    }
}
