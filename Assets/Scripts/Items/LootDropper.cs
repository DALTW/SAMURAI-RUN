using UnityEngine;
using SamuraiRunner.Enemies;
using SamuraiRunner.Audio;

namespace SamuraiRunner.Items
{
    /// <summary>
    /// 적이 처치되면 아이템을 떨어뜨립니다 (궁수·닌자 → 주먹밥).
    /// 같은 오브젝트의 ThrowerEnemy.Killed 이벤트를 구독합니다.
    /// 드롭 확률·아이템·드롭 위치는 프리팹에서 조절합니다.
    /// </summary>
    [RequireComponent(typeof(ThrowerEnemy))]
    public class LootDropper : MonoBehaviour
    {
        [Tooltip("떨어뜨릴 아이템 프리팹 (Pickup_Onigiri)")]
        public HealthPickup dropPrefab;
        [Tooltip("드롭 확률 (1 = 항상, 0.5 = 절반)")]
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f;
        [Tooltip("아이템이 나타나는 위치 (적의 발 기준 오프셋)")]
        [SerializeField] private Vector2 dropOffset = new Vector2(0f, 0.6f);

        private ThrowerEnemy enemy;

        private void Awake()
        {
            enemy = GetComponent<ThrowerEnemy>();
        }

        private void OnEnable()
        {
            if (enemy != null) enemy.Killed += Drop;
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.Killed -= Drop;
        }

        /// <summary>아이템을 떨어뜨립니다 (처치 시 자동 호출, 테스트용으로 직접 호출해도 됨).</summary>
        public void Drop()
        {
            if (dropPrefab == null) return;
            if (dropChance < 1f && Random.value > dropChance) return;

            Vector3 feet = transform.position;
            Vector3 spawn = feet + (Vector3)dropOffset;
            HealthPickup item = Instantiate(dropPrefab, spawn, Quaternion.identity);
            item.Launch(feet.y); // 적의 발 높이 = 바닥 (레이로 바닥을 못 찾을 때 대비)
            Sfx.Play(SfxId.PickupDrop);
        }
    }
}
