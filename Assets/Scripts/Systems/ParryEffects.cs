using UnityEngine;
using SamuraiRunner.Effects;
using SamuraiRunner.Player;

namespace SamuraiRunner.Systems
{
    /// <summary>
    /// 패링 성공 시 연출 담당: 히트스톱 + 칼빛 스파크.
    /// 저스트 패링은 길고 화려하게, 일반 패링은 짧고 담백하게.
    /// </summary>
    public class ParryEffects : MonoBehaviour
    {
        [Tooltip("플레이어의 PlayerAttack (씬 자동 구성이 연결)")]
        public PlayerAttack playerAttack;
        [Tooltip("칼빛 스파크 프리팹")]
        public FlashEffect sparkPrefab;

        [Header("히트스톱")]
        [Tooltip("저스트 패링 히트스톱 시간(초)")]
        [SerializeField] private float justHitStop = 0.2f;
        [Tooltip("일반 패링 히트스톱 시간(초)")]
        [SerializeField] private float normalHitStop = 0.04f;

        [Header("스파크")]
        [SerializeField] private Color justColor = new Color(1f, 0.95f, 0.6f);
        [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.9f);
        [SerializeField] private float justScale = 1.8f;
        [SerializeField] private float normalScale = 1f;

        private void OnEnable()
        {
            if (playerAttack != null) playerAttack.ParrySucceeded += HandleParry;
        }

        private void OnDisable()
        {
            if (playerAttack != null) playerAttack.ParrySucceeded -= HandleParry;
        }

        private void HandleParry(bool isJust, Vector2 position)
        {
            if (HitStop.Instance != null)
                HitStop.Instance.Do(isJust ? justHitStop : normalHitStop);

            if (sparkPrefab != null)
            {
                var fx = Instantiate(sparkPrefab, position,
                    Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f)));
                fx.Init(isJust ? justColor : normalColor, isJust ? justScale : normalScale);
            }
        }
    }
}
