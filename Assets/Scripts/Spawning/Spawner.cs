using System;
using System.Collections.Generic;
using UnityEngine;

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

        [NonSerialized] public float timer;
    }

    /// <summary>
    /// 플레이어 앞쪽에 적·장애물을 주기적으로 스폰합니다.
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

        private float elapsed;

        private void Start()
        {
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
                if (entry.timer <= 0f)
                {
                    entry.timer = UnityEngine.Random.Range(entry.minInterval, entry.maxInterval);
                    Vector3 pos = new Vector3(player.position.x + spawnAheadDistance, entry.spawnY, 0f);
                    Instantiate(entry.prefab, pos, Quaternion.identity);
                }
            }
        }
    }
}
