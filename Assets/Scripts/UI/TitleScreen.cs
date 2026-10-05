using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SamuraiRunner.Audio;

namespace SamuraiRunner.UI
{
    /// <summary>
    /// 시작 화면 담당: 시작 버튼(마우스 클릭) 또는 Enter·Space·Z·X 키로 게임 씬을 불러옵니다.
    /// 넘어갈 때 화면을 검게 덮었다가 불러옵니다. 시작 화면 UI는 10단계 메뉴가 만듭니다.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("연결")]
        public Button startButton;
        [Tooltip("시작할 때 화면을 검게 덮는 이미지 (비워두면 바로 넘어감)")]
        public Image fadeImage;

        [Header("시작")]
        [Tooltip("시작하면 불러올 게임 씬 이름 (Build Settings에 들어 있어야 함)")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [Tooltip("체크하면 Enter·Space·Z·X 키로도 시작합니다")]
        [SerializeField] private bool keyboardStart = true;
        [Tooltip("화면이 검게 덮이는 시간(초)")]
        [SerializeField] private float fadeTime = 0.35f;

        private bool starting;

        private void Awake()
        {
            Time.timeScale = 1f; // 혹시 멈춘 채로 넘어왔어도 정상 속도로
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (fadeImage != null)
            {
                Color c = fadeImage.color;
                c.a = 0f;
                fadeImage.color = c;
                fadeImage.raycastTarget = false;
            }
        }

        private void Start()
        {
            // 키보드·패드로 바로 누를 수 있게 시작 버튼을 선택해 둠
            if (startButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        private void Update()
        {
            if (!keyboardStart || starting) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)
                StartGame();
        }

        /// <summary>게임을 시작합니다 (버튼에 연결됨). 여러 번 눌려도 한 번만 넘어갑니다.</summary>
        public void StartGame()
        {
            if (starting) return;
            starting = true;
            Sfx.Play(SfxId.UiStart); // 칼 뽑는 소리 (효과음 재생기는 씬이 바뀌어도 유지돼 끊기지 않음)
            StartCoroutine(FadeAndLoad());
        }

        private IEnumerator FadeAndLoad()
        {
            if (fadeImage != null && fadeTime > 0f)
            {
                fadeImage.raycastTarget = true; // 넘어가는 동안 다른 클릭 막기
                Color c = fadeImage.color;
                float t = 0f;
                while (t < fadeTime)
                {
                    t += Time.unscaledDeltaTime;
                    c.a = Mathf.Clamp01(t / fadeTime);
                    fadeImage.color = c;
                    yield return null;
                }
            }
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
