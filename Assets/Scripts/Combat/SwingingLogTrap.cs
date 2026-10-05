using UnityEngine;
using SamuraiRunner.Common;
using SamuraiRunner.Player;
using SamuraiRunner.Audio;

namespace SamuraiRunner.Combat
{
    /// <summary>
    /// 위에서 휘둘러 내려오는 통나무 함정 (밧줄 두 개로 수평으로 매단 공성추).
    /// 스폰 위치가 밧줄을 매단 지점(화면 위)이고, 처음에는 통나무가 화면 밖 위쪽에 들려 있습니다.
    /// 플레이어가 다가오면 놓여서 진자처럼 휘둘러 내려와 몸통 높이로 지나갑니다.
    /// 놓는 시점은 플레이어 속도로 계산해, 플레이어가 매단 지점 아래에 올 때 통나무가 맨 아래를 지나게 합니다.
    /// 칼에 맞으면(패링) 반대쪽으로 튕겨 나가고 더는 피해를 주지 않습니다. 저스트 패링이면 더 세게 튕겨 나갑니다.
    /// 칼 판정(PlayerAttack)에 걸리도록 이 오브젝트는 Projectile 레이어에 둡니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class SwingingLogTrap : MonoBehaviour, IParryable
    {
        public enum State { Waiting, Swinging, Deflected }

        [Header("흔들림")]
        [Tooltip("통나무가 맨 아래로 내려왔을 때 중심 높이(월드 Y) — 플레이어 몸통 높이. 밧줄 길이는 매단 높이(스폰 Y)에서 자동 계산")]
        [SerializeField] private float bottomY = -0.85f;
        [Tooltip("처음 들려 있는 각도(도). 90이면 밧줄이 수평 → 화면 위에서 휘둘러 내려옵니다")]
        [SerializeField] private float startAngle = 90f;
        [Tooltip("흔들림 중력 (클수록 빠르게 휘둘러짐). 25면 놓은 뒤 약 0.8초에 맨 아래")]
        [SerializeField] private float swingGravity = 25f;

        [Header("발동")]
        [Tooltip("0이면 플레이어가 매단 지점에 올 때 통나무가 맨 아래를 지납니다. 양수면 그만큼(초) 더 일찍 놓음")]
        [SerializeField] private float releaseLeadTime = 0f;

        [Header("밧줄")]
        [Tooltip("두 밧줄 사이 간격 (유닛)")]
        [SerializeField] private float ropeSpacing = 0.9f;
        [Tooltip("통나무 중심 기준 밧줄이 묶이는 높이 (유닛) — 통나무 윗면")]
        [SerializeField] private float ropeAttachY = 0.0625f;
        [SerializeField] private Color ropeLight = new Color32(0xD9, 0xB7, 0x7A, 0xFF);
        [SerializeField] private Color ropeDark = new Color32(0xA8, 0x83, 0x4D, 0xFF);
        [SerializeField] private Color ropeShadow = new Color32(0x45, 0x2B, 0x1F, 0xFF);

        [Header("피해")]
        [SerializeField] private int damage = 1;

        [Header("튕겨내기 (패링)")]
        [Tooltip("일반 패링: 되돌아가는 속도 배율")]
        [SerializeField] private float normalKnockback = 1f;
        [Tooltip("저스트 패링: 되돌아가는 속도 배율")]
        [SerializeField] private float justKnockback = 1.4f;
        [Tooltip("튕겨낼 때 최소 회전 속도(rad/s) — 흔들림 끝에서 맞아도 확실히 밀려나게")]
        [SerializeField] private float minKnockSpeed = 2.5f;
        [SerializeField] private Color deflectedColor = new Color(0.7f, 0.7f, 0.75f);
        [SerializeField] private Color reflectedColor = new Color(1f, 0.9f, 0.4f);
        [Tooltip("튕겨낸 뒤 색이 원래대로 돌아오는 시간(초)")]
        [SerializeField] private float tintTime = 0.4f;
        [Tooltip("튕겨낸 뒤 이 시간(초)이 지나면 정리")]
        [SerializeField] private float deflectedLifetime = 2.5f;

        public State CurrentState => state;

        private State state = State.Waiting;
        private SpriteRenderer sr;
        private Collider2D col;
        private Vector3 pivot;
        private float ropeLength;
        private float theta;   // 밧줄 각도 (라디안, 양수 = 매단 지점의 오른쪽)
        private float omega;   // 각속도 (라디안/초)
        private float timeToBottom;
        private Transform player;
        private Rigidbody2D playerBody;
        private Transform[] ropes;
        private Texture2D ropeTexture;
        private Sprite ropeSprite;
        private Color tint = Color.white;
        private float tintTimer;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();

            pivot = transform.position; // 스폰 위치 = 밧줄을 매단 지점
            ropeLength = Mathf.Max(0.5f, pivot.y - bottomY - ropeAttachY);
            theta = startAngle * Mathf.Deg2Rad;
            omega = 0f;
            timeToBottom = SimulateTimeToBottom();

            var motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                player = motor.transform;
                playerBody = motor.GetComponent<Rigidbody2D>();
            }

            CreateRopes();
            ApplyPose();
        }

        private void Update()
        {
            if (state == State.Waiting)
            {
                if (!ShouldRelease()) return;
                state = State.Swinging;
                Sfx.Play(SfxId.LogSwing); // 밧줄 삐걱 + 휘이익
            }

            float dt = Time.deltaTime; // 히트스톱(timeScale 0) 중에는 같이 멈춤
            if (dt > 0f) Integrate(dt);
            ApplyPose();
            UpdateTint(dt);
        }

        /// <summary>플레이어가 지금 속도로 달려 매단 지점에 도착할 때 통나무가 맨 아래를 지나도록 놓습니다.</summary>
        private bool ShouldRelease()
        {
            if (player == null) return false;
            float distance = pivot.x - player.position.x;
            if (distance <= 0f) return true; // 이미 지나쳤으면 그냥 놓음 (뒤에서 흔들리다 정리됨)

            float speed = playerBody != null ? playerBody.linearVelocity.x : 0f;
            if (speed <= 0.01f) return false;
            return distance <= speed * (timeToBottom + releaseLeadTime);
        }

        private void Integrate(float dt)
        {
            const int steps = 4;
            float h = dt / steps;
            float k = swingGravity / ropeLength;
            for (int i = 0; i < steps; i++)
            {
                omega += -k * Mathf.Sin(theta) * h;
                theta += omega * h;
            }
        }

        /// <summary>밧줄 두 개가 평행하게 매달려 통나무는 늘 수평을 유지합니다.</summary>
        private void ApplyPose()
        {
            transform.position = new Vector3(
                pivot.x + ropeLength * Mathf.Sin(theta),
                pivot.y - ropeLength * Mathf.Cos(theta) - ropeAttachY,
                pivot.z);

            if (ropes == null) return;
            Quaternion rot = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
            foreach (Transform rope in ropes)
                if (rope != null) rope.localRotation = rot;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHurt(other);
        private void OnTriggerStay2D(Collider2D other) => TryHurt(other);

        private void TryHurt(Collider2D other)
        {
            if (state != State.Swinging) return;
            if (other.TryGetComponent<PlayerHealth>(out var health))
                if (health.TakeDamage(damage)) // 무적 중이면 아무 일도 없음
                    Sfx.Play(SfxId.LogHit, 0.8f);
        }

        public void OnParried(GameObject attacker, bool isJustParry)
        {
            if (state != State.Swinging) return;
            state = State.Deflected;

            // 플레이어 반대쪽으로 되돌려 보냄 (지금 속도보다 약하지 않게)
            float away = attacker != null && attacker.transform.position.x > transform.position.x ? -1f : 1f;
            float speed = Mathf.Max(Mathf.Abs(omega), minKnockSpeed);
            omega = away * speed * (isJustParry ? justKnockback : normalKnockback);

            tint = isJustParry ? reflectedColor : deflectedColor;
            tintTimer = tintTime;
            Sfx.Play(SfxId.LogHit); // 칼에 맞은 나무 소리 (칼의 금속음은 PlayerAttack이 냄)
            if (col != null) col.enabled = false; // 더는 칼·몸에 걸리지 않게
            Destroy(gameObject, deflectedLifetime);
        }

        private void UpdateTint(float dt)
        {
            if (tintTimer <= 0f) return;
            tintTimer -= dt;
            sr.color = Color.Lerp(Color.white, tint, Mathf.Clamp01(tintTimer / tintTime));
        }

        private float SimulateTimeToBottom()
        {
            float th = startAngle * Mathf.Deg2Rad, om = 0f, t = 0f;
            const float h = 1f / 240f;
            float k = swingGravity / ropeLength;
            while (th > 0f && t < 5f)
            {
                om += -k * Mathf.Sin(th) * h;
                th += om * h;
                t += h;
            }
            return t;
        }

        private void CreateRopes()
        {
            float ppu = sr.sprite != null ? sr.sprite.pixelsPerUnit : 32f;

            // 2x4 픽셀 꼬인 밧줄 무늬 (아래 줄부터), 세로로 반복
            ropeTexture = new Texture2D(2, 4, TextureFormat.RGBA32, false)
            {
                name = "SwingLogRope",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            ropeTexture.SetPixels(new[]
            {
                ropeDark, ropeShadow,
                ropeLight, ropeDark,
                ropeDark, ropeLight,
                ropeShadow, ropeDark,
            });
            ropeTexture.Apply(false, true);
            ropeSprite = Sprite.Create(ropeTexture, new Rect(0, 0, 2, 4), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);

            ropes = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "RopeLeft" : "RopeRight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i == 0 ? -0.5f : 0.5f) * ropeSpacing, ropeAttachY, 0f);

                var rope = go.AddComponent<SpriteRenderer>();
                rope.sprite = ropeSprite;
                rope.sharedMaterial = sr.sharedMaterial;
                rope.sortingLayerID = sr.sortingLayerID;
                rope.sortingOrder = sr.sortingOrder - 1; // 통나무 뒤
                rope.drawMode = SpriteDrawMode.Tiled;
                rope.size = new Vector2(2f / ppu, ropeLength);
                ropes[i] = go.transform;
            }
        }

        private void OnDestroy()
        {
            if (ropeSprite != null) Destroy(ropeSprite);
            if (ropeTexture != null) Destroy(ropeTexture);
        }

        private void OnDrawGizmosSelected()
        {
            // 에디터에서 고르면 흔들리는 궤적을 보여줌 (플레이 전에는 현재 위치를 매단 지점으로 봄)
            Vector3 p = Application.isPlaying ? pivot : transform.position;
            float len = Application.isPlaying ? ropeLength : Mathf.Max(0.5f, p.y - bottomY - ropeAttachY);
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Vector3 prev = p + new Vector3(len * Mathf.Sin(-startAngle * Mathf.Deg2Rad), -len * Mathf.Cos(startAngle * Mathf.Deg2Rad) - ropeAttachY);
            for (int i = 1; i <= 24; i++)
            {
                float a = Mathf.Lerp(-startAngle, startAngle, i / 24f) * Mathf.Deg2Rad;
                Vector3 q = p + new Vector3(len * Mathf.Sin(a), -len * Mathf.Cos(a) - ropeAttachY);
                Gizmos.DrawLine(prev, q);
                prev = q;
            }
            Gizmos.DrawWireSphere(p, 0.1f);
        }
    }
}
