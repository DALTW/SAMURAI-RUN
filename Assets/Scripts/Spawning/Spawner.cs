using System;
using System.Collections.Generic;
using UnityEngine;
using SamuraiRunner.Environment;
using SamuraiRunner.Player;

namespace SamuraiRunner.Spawning
{
    /// <summary>
    /// 스폰 항목 하나. 어떤 프리팹을 얼마나 자주, 어느 높이에, 언제부터 스폰할지
    /// 전부 Scene의 Inspector에서 조절합니다.
    /// </summary>
    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("알아보기 쉬운 이름")]
        public string name;
        public GameObject prefab;
        [Tooltip("스폰 간격 최소(초)")]
        public float minInterval = 3f;
        [Tooltip("스폰 간격 최대(초)")]
        public float maxInterval = 5f;
        [Tooltip("스폰 위치 Y (월드 좌표)")]
        public float spawnY = -1f;
        [Tooltip("게임 시작 후 이 시간(초)이 지나야 스폰 시작 — 난이도 단계에 활용")]
        public float startDelay = 0f;
        public bool enabled = true;
        [Tooltip("체크하면 구덩이 근처에도 스폰합니다 (공중에 떠 있는 것 등). 기본은 구덩이 근처를 피함")]
        public bool allowNearPits;

        [NonSerialized] public float timer;
    }

    /// <summary>
    /// 플레이어 앞쪽에 적·장애물을 주기적으로 스폰합니다.
    /// 바닥 생성기(GroundGenerator)가 있으면 구덩이 근처에는 스폰하지 않고, 지나갈 때까지 잠시 미룹니다.
    /// 항목 추가/삭제/수치 조정은 전부 Scene에서 가능합니다.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Tooltip("플레이어 Transform (씬 자동 구성이 연결)")]
        public Transform player;
        [Tooltip("플레이어보다 얼마나 앞(오른쪽)에 스폰할지 (유닛)")]
        [SerializeField] private float spawnAheadDistance = 14f;

        [Tooltip("스폰 항목 목록")]
        public List<SpawnEntry> entries = new List<SpawnEntry>();

        [Header("구덩이 피하기")]
        [Tooltip("바닥 생성기 (비워두면 자동으로 찾음). 없으면 구덩이 검사 없이 스폰합니다")]
        public GroundGenerator ground;
        [Tooltip("구덩이 가장자리에서 이 거리(유닛) 안에는 장애물·적을 만들지 않습니다 (점프 도움닫기·착지 공간)")]
        [SerializeField] private float pitClearance = 3.5f;
        [Tooltip("연달아 스폰되는 두 오브젝트 사이 최소 간격(유닛) — 미뤄진 스폰이 한곳에 겹치지 않게")]
        [SerializeField] private float minSpawnGap = 3f;
        [Tooltip("바로 앞 스폰과 너무 가까워 미뤄질 때, 다시 시도하기 전 무작위로 기다리는 최대 시간(초)")]
        [SerializeField] private float postponeJitter = 0.6f;

        [Header("속도에 맞춘 여유")]
        [Tooltip("체크하면 플레이어가 빨라진 비율만큼 구덩이 여유 거리·최소 간격도 늘립니다 (반응 시간 유지)")]
        [SerializeField] private bool scaleGapsWithSpeed = true;
        [Tooltip("위 여유 거리들이 그대로 쓰이는 기준 달리기 속도 (유닛/초)")]
        [SerializeField] private float referenceSpeed = 5f;

        private float elapsed;
        private float lastSpawnX = float.NegativeInfinity;
        private PlayerMotor playerMotor;
        private readonly Dictionary<GameObject, float> halfWidthCache = new Dictionary<GameObject, float>();

        /// <summary>기준 속도보다 빠르면 그 비율 (느리면 1)</summary>
        private float GapScale =>
            scaleGapsWithSpeed && playerMotor != null && referenceSpeed > 0f
                ? Mathf.Max(1f, playerMotor.RunSpeed / referenceSpeed)
                : 1f;

        private void Start()
        {
            if (ground == null) ground = FindAnyObjectByType<GroundGenerator>();
            if (player != null) playerMotor = player.GetComponent<PlayerMotor>();

            foreach (var entry in entries)
                entry.timer = UnityEngine.Random.Range(entry.minInterval, entry.maxInterval);
        }

        private void Update()
        {
            if (player == null) return;

            elapsed += Time.deltaTime;

            foreach (var entry in entries)
            {
                if (!entry.enabled || entry.prefab == null || elapsed < entry.startDelay)
                    continue;

                entry.timer -= Time.deltaTime;
                if (entry.timer > 0f) continue;

                float x = player.position.x + spawnAheadDistance;
                if (x - lastSpawnX < minSpawnGap * GapScale)
                {
                    // 바로 앞 스폰과 겹침 → 조금만 더 달린 뒤 다시 시도
                    entry.timer = UnityEngine.Random.Range(0f, postponeJitter);
                    continue;
                }
                if (!IsClearOfPits(entry, x))
                {
                    // 구덩이 근처 → 이 항목의 최소 간격 안에서 무작위로 다시 잡음.
                    // 짧게만 미루면 미뤄진 스폰들이 구덩이 바로 뒤(착지 지점)에 매번 몰려 나옵니다.
                    entry.timer = UnityEngine.Random.Range(postponeJitter, Mathf.Max(postponeJitter, entry.minInterval));
                    continue;
                }

                entry.timer = UnityEngine.Random.Range(entry.minInterval, entry.maxInterval);
                Instantiate(entry.prefab, new Vector3(x, entry.spawnY, 0f), Quaternion.identity);
                lastSpawnX = x;
            }
        }

        private bool IsClearOfPits(SpawnEntry entry, float x)
        {
            if (ground == null || entry.allowNearPits) return true;

            float half = GetHalfWidth(entry.prefab);
            return ground.IsClearOfPits(x - half, x + half, pitClearance * GapScale);
        }

        /// <summary>프리팹의 가로 절반 폭 (콜라이더 기준, 없으면 스프라이트 기준)</summary>
        private float GetHalfWidth(GameObject prefab)
        {
            if (halfWidthCache.TryGetValue(prefab, out float half)) return half;

            half = 0.5f;
            float scaleX = Mathf.Abs(prefab.transform.localScale.x);
            if (prefab.TryGetComponent<BoxCollider2D>(out var box))
                half = (Mathf.Abs(box.offset.x) + box.size.x * 0.5f) * scaleX;
            else if (prefab.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
                half = sr.sprite.bounds.extents.x * scaleX;

            halfWidthCache[prefab] = half;
            return half;
        }
    }
}
