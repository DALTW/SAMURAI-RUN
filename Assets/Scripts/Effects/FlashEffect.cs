using UnityEngine;

namespace SamuraiRunner.Effects
{
    /// <summary>
    /// 짧게 번쩍이고 사라지는 이펙트 (칼빛·스파크·적 사망 등 공용).
    /// 히트스톱(timeScale=0) 중에도 재생되도록 unscaled 시간을 사용합니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FlashEffect : MonoBehaviour
    {
        [Tooltip("이펙트 지속 시간(초)")]
        [SerializeField] private float duration = 0.18f;
        [Tooltip("재생 동안 커지는 배율")]
        [SerializeField] private float growFactor = 1.4f;

        private SpriteRenderer sr;
        private Vector3 baseScale;
        private float t;

        /// <summary>색과 크기 배율을 지정합니다 (스폰 직후 호출).</summary>
        public void Init(Color color, float scaleMultiplier)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            sr.color = color;
            transform.localScale *= scaleMultiplier;
            baseScale = transform.localScale;
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            transform.localScale = baseScale * Mathf.Lerp(1f, growFactor, k);
            Color c = sr.color;
            c.a = 1f - k;
            sr.color = c;

            if (k >= 1f)
                Destroy(gameObject);
        }
    }
}
