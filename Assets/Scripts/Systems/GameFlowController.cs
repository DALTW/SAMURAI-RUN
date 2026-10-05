using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SamuraiRunner.Player;
using SamuraiRunner.Audio;

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
        [Tooltip("쓰러진 뒤 게임오버 징 소리가 나기까지의 시간(초) — 쓰러지는 소리와 겹치지 않게")]
        [SerializeField] private float gameOverSoundDelay = 0.4f;
        [Tooltip("쓰러졌을 때 배경음악을 이 크기(0~1)로 줄임 — 다시 달리면 원래 크기로 돌아옴")]
        [Range(0f, 1f)] [SerializeField] private float deathMusicVolume = 0.35f;

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

            if (playerMotor != null)
            {
                playerMotor.StopRunning();
                playerMotor.enabled = false; // 쓰러진 뒤에는 점프 입력도 받지 않음 (점프 소리가 나지 않게)
            }
            if (playerAttack != null) playerAttack.enabled = false;
            if (gameOverUI != null) gameOverUI.SetActive(true);

            StartCoroutine(FadeToAsh());
            StartCoroutine(PlayGameOverSound());
            Music.Duck(deathMusicVolume, deathFadeTime);
        }

        private IEnumerator PlayGameOverSound()
        {
            yield return new WaitForSecondsRealtime(gameOverSoundDelay);
            Sfx.Play(SfxId.GameOver);
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
