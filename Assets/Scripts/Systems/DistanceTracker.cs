using UnityEngine;

namespace SamuraiRunner.Systems
{
    /// <summary>
    /// 플레이어가 달린 거리를 추적합니다. (점수·배경 전환의 기반)
    /// </summary>
    public class DistanceTracker : MonoBehaviour
    {
        [Tooltip("플레이어 Transform (씬 자동 구성이 연결)")]
        public Transform player;
        [Tooltip("1유닛을 몇 미터로 표시할지")]
        [SerializeField] private float metersPerUnit = 1f;

        private float startX;
        private bool initialized;

        public float DistanceMeters =>
            initialized && player != null
                ? Mathf.Max(0f, (player.position.x - startX) * metersPerUnit)
                : 0f;

        private void Start()
        {
            if (player != null)
            {
                startX = player.position.x;
                initialized = true;
            }
        }
    }
}
