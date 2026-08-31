using UnityEngine;

namespace SamuraiRunner.Systems
{
    /// <summary>
    /// 히트스톱(화면 멈칫) 담당. 저스트 패링 성공 시 짧게 시간을 멈춰 손맛을 살립니다.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        private float remaining;
        private float originalScale = 1f;
        private bool active;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>지정한 시간(초, 실제 시간 기준)만큼 게임을 멈춥니다.</summary>
        public void Do(float duration)
        {
            if (duration <= 0f) return;

            if (!active)
            {
                originalScale = Time.timeScale;
                active = true;
                Time.timeScale = 0f;
            }
            remaining = Mathf.Max(remaining, duration);
        }

        private void Update()
        {
            if (!active) return;

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                Time.timeScale = originalScale;
                active = false;
            }
        }

        private void OnDestroy()
        {
            if (active) Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }
    }
}
