using UnityEngine;
using SamuraiRunner.Player;

namespace SamuraiRunner.Common
{
    /// <summary>
    /// 플레이어보다 일정 거리 뒤로 밀려나면 스스로 제거되는 정리용 컴포넌트.
    /// 적·장애물 프리팹에 붙입니다.
    /// </summary>
    public class DespawnBehind : MonoBehaviour
    {
        [Tooltip("플레이어 뒤로 이 거리(유닛) 이상 밀려나면 제거")]
        [SerializeField] private float distanceBehind = 15f;

        private Transform player;

        private void Start()
        {
            var motor = Object.FindFirstObjectByType<PlayerMotor>();
            if (motor != null) player = motor.transform;
        }

        private void Update()
        {
            if (player != null && player.position.x - transform.position.x > distanceBehind)
                Destroy(gameObject);
        }
    }
}
