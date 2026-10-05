using System.Collections.Generic;
using UnityEngine;
using SamuraiRunner.Player;

namespace SamuraiRunner.Environment
{
    /// <summary>
    /// 달리는 동안 플레이어 앞쪽으로 바닥 조각을 계속 이어 붙이고, 조각 사이에 구덩이(낙사 구간)를 만듭니다.
    /// 이 컴포넌트를 붙인 오브젝트(Ground)의 SpriteRenderer·BoxCollider2D를 "견본"으로 씁니다.
    /// 플레이 중에는 견본을 숨기고 같은 모양의 조각을 만들며, 에디터에서는 원래 긴 바닥이 그대로 보입니다.
    /// 지나간 조각은 지우므로 끝없이 달릴 수 있습니다.
    /// Spawner가 구덩이 위치를 물어보고, 구덩이 근처에는 장애물·적을 만들지 않습니다.
    /// </summary>
    public class GroundGenerator : MonoBehaviour
    {
        [Header("연결 (비워두면 자동으로 찾음)")]
        [SerializeField] private Transform player;

        [Header("구덩이")]
        [Tooltip("시작 지점에서 이 거리(유닛)까지는 구덩이를 만들지 않습니다")]
        [SerializeField] private float safeStartDistance = 35f;
        [Tooltip("구덩이와 구덩이 사이 바닥 길이 (x = 최소, y = 최대, 유닛)")]
        [SerializeField] private Vector2 groundLengthRange = new Vector2(14f, 26f);
        [Tooltip("구덩이 폭 (x = 최소, y = 최대, 유닛). 길게 누른 점프의 비거리가 약 3.4유닛이므로 그보다 충분히 작게")]
        [SerializeField] private Vector2 pitWidthRange = new Vector2(1.6f, 2.4f);
        [Tooltip("구덩이 속 어둠이 내려가는 깊이 (유닛)")]
        [SerializeField] private float pitDepth = 8f;

        [Header("생성 범위")]
        [Tooltip("플레이어 앞 이 거리(유닛)까지 미리 만들어 둡니다. Spawner의 스폰 거리 + 구덩이 여유보다 커야 합니다")]
        [SerializeField] private float generateAhead = 30f;
        [Tooltip("플레이어 뒤로 이 거리(유닛)보다 멀어진 조각은 지웁니다")]
        [SerializeField] private float keepBehind = 20f;

        [Header("속도에 맞춘 간격")]
        [Tooltip("체크하면 플레이어가 빨라진 비율만큼 구덩이 사이 바닥도 길어져, 초당 구덩이 수가 일정하게 유지됩니다")]
        [SerializeField] private bool scaleSpacingWithSpeed = true;
        [Tooltip("위 바닥 길이가 그대로 쓰이는 기준 달리기 속도 (유닛/초)")]
        [SerializeField] private float referenceSpeed = 5f;

        [Header("구덩이 색 (위 → 아래)")]
        [SerializeField] private Color pitTopColor = new Color32(0x16, 0x12, 0x22, 0xFF);
        [SerializeField] private Color pitMidColor = new Color32(0x0E, 0x07, 0x1B, 0xFF);
        [SerializeField] private Color pitBottomColor = new Color32(0x08, 0x04, 0x0F, 0xFF);

        /// <summary>바닥 윗면의 월드 Y (견본 콜라이더의 윗면)</summary>
        public float GroundTopY => groundTopY;
        /// <summary>이 X까지 바닥·구덩이가 정해져 있습니다</summary>
        public float GeneratedUntilX => generatedUntil;
        /// <summary>현재 남아 있는 구덩이들 (x = 왼쪽 끝, y = 오른쪽 끝)</summary>
        public IReadOnlyList<Vector2> Pits => pits;

        private struct Piece
        {
            public GameObject go;
            public float rightX;
        }

        private readonly Queue<Piece> pieces = new Queue<Piece>();
        private readonly List<Vector2> pits = new List<Vector2>();

        private SpriteRenderer templateRenderer;
        private BoxCollider2D templateCollider;
        private Transform root;
        private PhysicsMaterial2D noFriction;
        private Texture2D pitTexture;
        private Sprite pitSprite;
        private float groundTopY = -1.5f;
        private float thickness = 2f;
        private float ppu = 32f;
        private float generatedUntil = float.NegativeInfinity;
        private float firstPitX;
        private bool active;
        private PlayerMotor playerMotor;

        /// <summary>기준 속도보다 빠르면 그 비율 (느리면 1)</summary>
        private float SpacingScale =>
            scaleSpacingWithSpeed && playerMotor != null && referenceSpeed > 0f
                ? Mathf.Max(1f, playerMotor.RunSpeed / referenceSpeed)
                : 1f;

        private void Awake()
        {
            if (player == null)
            {
                var motor = FindAnyObjectByType<PlayerMotor>();
                if (motor != null) player = motor.transform;
            }
            if (player != null) playerMotor = player.GetComponent<PlayerMotor>();

            templateRenderer = GetComponent<SpriteRenderer>();
            templateCollider = GetComponent<BoxCollider2D>();
            if (player == null || templateCollider == null)
            {
                Debug.LogWarning("[GroundGenerator] 플레이어나 견본 바닥(BoxCollider2D)을 찾지 못해 원래 바닥을 그대로 둡니다.", this);
                return;
            }

            // 바닥 윗면·두께는 견본 콜라이더 기준 (씬에서 Ground를 옮기면 같이 따라감)
            Vector3 scale = transform.lossyScale;
            thickness = templateCollider.size.y * Mathf.Abs(scale.y);
            groundTopY = transform.position.y + (templateCollider.offset.y * scale.y) + thickness * 0.5f;
            if (templateRenderer != null && templateRenderer.sprite != null)
                ppu = templateRenderer.sprite.pixelsPerUnit;

            // 구덩이 벽에 몸을 밀어붙인 채 매달리지 않도록 바닥 조각은 마찰 0
            noFriction = new PhysicsMaterial2D("GroundNoFriction")
            {
                friction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum, // 상대(플레이어) 마찰과 섞여도 0이 되게
            };
            pitSprite = CreatePitSprite();
            root = new GameObject("Ground (generated)").transform;

            // 견본은 숨기고, 첫 프레임 물리보다 먼저 시작 구간을 만들어 둠
            if (templateRenderer != null) templateRenderer.enabled = false;
            templateCollider.enabled = false;

            float startX = player.position.x;
            generatedUntil = Snap(startX - keepBehind - 5f);
            firstPitX = Snap(startX + safeStartDistance + Random.Range(0f, 5f));
            active = true;
            GenerateUntil(startX + generateAhead);
        }

        private void Update()
        {
            if (!active || player == null) return;

            float px = player.position.x;
            GenerateUntil(px + generateAhead);

            // 지나간 조각·구덩이 정리
            float limit = px - keepBehind;
            while (pieces.Count > 0 && pieces.Peek().rightX < limit)
            {
                Piece p = pieces.Dequeue();
                if (p.go != null) Destroy(p.go);
            }
            while (pits.Count > 0 && pits[0].y < limit) pits.RemoveAt(0);
        }

        /// <summary>
        /// [xMin, xMax] 구간이 구덩이 가장자리에서 margin 이상 떨어져 있고, 이미 만들어진 바닥 범위 안인지 확인합니다.
        /// 아직 정해지지 않은 구간이면 false (조금 뒤에 다시 물어보면 됨).
        /// </summary>
        public bool IsClearOfPits(float xMin, float xMax, float margin)
        {
            if (!active) return true;
            if (xMax + margin > generatedUntil) return false;
            for (int i = 0; i < pits.Count; i++)
            {
                Vector2 pit = pits[i];
                if (xMax + margin > pit.x && xMin - margin < pit.y) return false;
            }
            return true;
        }

        private void GenerateUntil(float targetX)
        {
            while (generatedUntil < targetX)
            {
                if (generatedUntil < firstPitX)
                {
                    AddGround(generatedUntil, firstPitX); // 시작 구간: 구덩이 없음
                    generatedUntil = firstPitX;
                    continue;
                }

                float spacing = SpacingScale; // 빨라지면 바닥도 길게 → 구덩이가 초 단위로 너무 잦아지지 않게
                float pitEnd = Snap(generatedUntil + Random.Range(pitWidthRange.x, pitWidthRange.y));
                float groundEnd = Snap(pitEnd + Random.Range(groundLengthRange.x * spacing, groundLengthRange.y * spacing));
                AddPit(generatedUntil, pitEnd);
                AddGround(pitEnd, groundEnd);
                generatedUntil = groundEnd;
            }
        }

        private void AddGround(float x0, float x1)
        {
            float width = x1 - x0;
            var go = new GameObject("GroundSegment") { layer = gameObject.layer };
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, groundTopY - thickness * 0.5f, transform.position.z);

            var sr = go.AddComponent<SpriteRenderer>();
            CopyLook(sr, 0);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(width, thickness);

            var col = go.AddComponent<BoxCollider2D>();
            col.offset = Vector2.zero;
            col.size = new Vector2(width, thickness);
            col.sharedMaterial = noFriction;

            pieces.Enqueue(new Piece { go = go, rightX = x1 });
        }

        private void AddPit(float x0, float x1)
        {
            pits.Add(new Vector2(x0, x1));

            // 구덩이 속 어둠: 1픽셀 폭 세로 그라데이션을 가로로 늘림. 양옆 바닥 뒤로 1픽셀씩 숨겨 틈이 안 보이게
            var go = new GameObject("Pit");
            go.transform.SetParent(root, false);
            float overlap = 1f / ppu;
            go.transform.position = new Vector3((x0 + x1) * 0.5f, groundTopY, transform.position.z);
            go.transform.localScale = new Vector3((x1 - x0 + overlap * 2f) * ppu, 1f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            CopyLook(sr, -1); // 바닥보다 한 칸 뒤, 배경보다는 앞
            sr.sprite = pitSprite;
            sr.color = Color.white;

            pieces.Enqueue(new Piece { go = go, rightX = x1 });
        }

        private void CopyLook(SpriteRenderer sr, int orderOffset)
        {
            if (templateRenderer == null) return;
            sr.sprite = templateRenderer.sprite;
            sr.sharedMaterial = templateRenderer.sharedMaterial;
            sr.color = templateRenderer.color;
            sr.sortingLayerID = templateRenderer.sortingLayerID;
            sr.sortingOrder = templateRenderer.sortingOrder + orderOffset;
        }

        /// <summary>위는 흙보다 약간 어둡고 아래로 갈수록 검어지는 계단식 픽셀 그라데이션 (피벗 = 윗변 중앙)</summary>
        private Sprite CreatePitSprite()
        {
            int h = Mathf.Max(8, Mathf.RoundToInt(pitDepth * ppu));
            pitTexture = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                name = "PitGradient",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var colors = new Color[h];
            for (int i = 0; i < h; i++)
            {
                int fromTop = h - 1 - i; // Texture2D는 아래 줄이 0번
                Color c;
                if (fromTop < 3) c = pitTopColor;
                else if (fromTop < 8) c = Color.Lerp(pitTopColor, pitMidColor, 0.5f);
                else if (fromTop < 20) c = pitMidColor;
                else if (fromTop < 36) c = Color.Lerp(pitMidColor, pitBottomColor, 0.5f);
                else c = pitBottomColor;
                colors[i] = c;
            }
            pitTexture.SetPixels(colors);
            pitTexture.Apply(false, true);

            return Sprite.Create(pitTexture, new Rect(0, 0, 1, h), new Vector2(0.5f, 1f), ppu, 0, SpriteMeshType.FullRect);
        }

        private float Snap(float x) => Mathf.Round(x * ppu) / ppu; // 픽셀 격자에 맞춰 가장자리가 반 픽셀에 걸리지 않게

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            if (pitSprite != null) Destroy(pitSprite);
            if (pitTexture != null) Destroy(pitTexture);
            if (noFriction != null) Destroy(noFriction);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || pits.Count == 0) return;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            foreach (Vector2 pit in pits)
                Gizmos.DrawWireCube(new Vector3((pit.x + pit.y) * 0.5f, groundTopY - 1f, 0f), new Vector3(pit.y - pit.x, 2f, 0f));
        }
    }
}
