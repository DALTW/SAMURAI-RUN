using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SamuraiRunner.Player;

namespace SamuraiRunner.Systems
{
    /// <summary>
    /// 게임의 흐름 담당: 사망 연출(멈춤 + 잿빛 페이드)과 재시작.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [Header("연결 (씬 자동 구성이 채움)")]
        public PlayerHealth playerHealth;
        public PlayerMotor playerMotor;
        public PlayerAttack playerAttack;
        [Tooltip("사망 시 켤 게임오버 UI 루트")]
        public GameObject gameOverUI;

        [Header("사망 연출")]
        [Tooltip("잿빛으로 물드는 시간(초)")]
        [SerializeField] private float deathFadeTime = 1.2f;
        [SerializeField] private Color deathTint = new Color(0.45f, 0.42f, 0.4f);

        [Header("재시작")]
        [SerializeField] private Key restartKey = Key.R;

        private bool gameOver;

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.Died += HandleDeath;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.Died -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (gameOver) return;
            gameOver = true;

            if (playerMotor != null) playerMotor.StopRunning();
            if (playerAttack != null) playerAttack.enabled = false;
            if (gameOverUI != null) gameOverUI.SetActive(true);

            StartCoroutine(FadeToAsh());
        }

        private IEnumerator FadeToAsh()
        {
            SpriteRenderer sr = playerHealth != null
                ? playerHealth.GetComponentInChildren<SpriteRenderer>()
                : null;
            if (sr == null) yield break;

            Color from = sr.color;
            float t = 0f;
            while (t < deathFadeTime)
            {
                t += Time.unscaledDeltaTime;
                sr.color = Color.Lerp(from, deathTint, t / deathFadeTime);
                yield return null;
            }
        }

        private void Update()
        {
            if (!gameOver) return;

            var kb = Keyboard.current;
            if (kb != null && kb[restartKey].wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }
}
