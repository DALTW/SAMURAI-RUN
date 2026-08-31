using UnityEngine;
using SamuraiRunner.Player;

namespace SamuraiRunner.Combat
{
    /// <summary>
    /// 플레이어가 닿으면 피해를 주는 컴포넌트. 장애물(바위·죽창)과 적 몸통에 붙입니다.
    /// 트리거 콜라이더와 함께 사용하세요.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DamageOnTouch : MonoBehaviour
    {
        [SerializeField] private int damage = 1;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerHealth>(out var health))
                health.TakeDamage(damage);
        }
    }
}
