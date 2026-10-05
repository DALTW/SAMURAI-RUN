using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SamuraiRunner.Player;
using SamuraiRunner.Systems;

namespace SamuraiRunner.UI
{
    /// <summary>
    /// 화면 표시 담당: 주먹밥(체력), 콤보, 달린 거리, 게임오버 기록(달린 거리·최대 콤보).
    /// 체력은 주먹밥 스프라이트 5단계(가득 → 빔)로 표시합니다. 심장을 하나 잃으면
    /// 해당 주먹밥이 중간 단계를 거쳐 빈 주먹밥으로 바뀌고, 회복하면 역순으로 채워지며 살짝 커졌다 돌아옵니다.
    /// 스프라이트가 없으면 색만 바꿉니다.
    /// UI 요소들은 씬 자동 구성이 만들며, 위치·크기는 Scene에서 수정하면 됩니다.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("연결 (씬 자동 구성이 채움)")]
        public PlayerHealth playerHealth;
        public ComboCounter comboCounter;
        public DistanceTracker distanceTracker;

        [Header("UI 요소")]
        [Tooltip("체력 아이콘(주먹밥) — 왼쪽부터 순서대로")]
        public Image[] heartIcons;
        public Text comboText;
        public Text distanceText;

        [Header("주먹밥 스프라이트")]
        [Tooltip("가득 찬 주먹밥 → 빈 주먹밥 순서 (Assets/Art/UI/hp_onigiri.png의 5장). 비워두면 아래 색으로만 표시")]
        public Sprite[] heartSprites;
        [Tooltip("심장을 잃거나 회복할 때 중간 단계를 거쳐 비워지고/채워지는 시간(초, 히트스톱 중에도 진행)")]
        [SerializeField] private float heartDrainTime = 0.35f;
        [Tooltip("회복할 때 주먹밥 아이콘이 커지는 배율")]
        [SerializeField] private float healPopScale = 1.3f;

        [Header("색 (스프라이트가 없을 때만 사용)")]
        [SerializeField] private Color heartFullColor = new Color(0.88f, 0.22f, 0.28f);
        [SerializeField] private Color heartEmptyColor = new Color(0.25f, 0.2f, 0.2f, 0.6f);

        [Header("게임오버 기록")]
        [Tooltip("죽었을 때 기록을 보여줄 텍스트 (비워두면 GameFlowController의 게임오버 UI에서 찾음)")]
        public Text gameOverText;
        [Tooltip("게임오버 문구. {0} = 달린 거리(m), {1} = 최대 콤보")]
        [TextArea(3, 8)]
        [SerializeField] private string gameOverFormat = "The Samurai's Run Has Ended\n\nDistance  {0} m\nMax Combo  {1}\n\nR - Run Again";

        private int shownHearts = -1;          // 현재 화면에 표시 중인 심장 수 (-1 = 아직 표시 전)
        private Coroutine[] drainRoutines;     // 아이콘별 "비워지는/채워지는" 연출
        private Vector2[] iconBasePos;         // 회복 연출 중 커질 때 원래 위치 (연출이 끝나면 복원)

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged += HandleHealthChanged;
                playerHealth.Died += HandleDied;
            }
            if (comboCounter != null) comboCounter.ComboChanged += HandleComboChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
                playerHealth.Died -= HandleDied;
            }
            if (comboCounter != null) comboCounter.ComboChanged -= HandleComboChanged;

            // 비활성화되면 코루틴은 자동으로 멈추므로, 커진 아이콘을 되돌리고 다음 갱신 때 즉시 반영되도록 초기화
            if (heartIcons != null)
                for (int i = 0; i < heartIcons.Length; i++) StopDrain(i);
            drainRoutines = null;
            shownHearts = -1;
        }

        private void Start()
        {
            if (playerHealth != null)
                HandleHealthChanged(playerHealth.CurrentHearts, playerHealth.MaxHearts);
            HandleComboChanged(comboCounter != null ? comboCounter.Combo : 0);
        }

        private void Update()
        {
            if (distanceText != null && distanceTracker != null)
                distanceText.text = $"{distanceTracker.DistanceMeters:0} m";
        }

        /// <summary>죽은 순간의 달린 거리·최대 콤보를 게임오버 문구에 채웁니다 (게임오버 UI는 GameFlowController가 켬).</summary>
        private void HandleDied()
        {
            Text target = gameOverText;
            if (target == null)
            {
                var flow = FindAnyObjectByType<GameFlowController>();
                if (flow != null && flow.gameOverUI != null)
                    target = flow.gameOverUI.GetComponentInChildren<Text>(true);
            }
            if (target == null) return;

            int meters = distanceTracker != null ? Mathf.FloorToInt(distanceTracker.DistanceMeters) : 0;
            int bestCombo = comboCounter != null ? comboCounter.BestCombo : 0;

            string message;
            try { message = string.Format(gameOverFormat, meters, bestCombo); }
            catch (System.FormatException)
            {
                message = $"The Samurai's Run Has Ended\n\nDistance  {meters} m\nMax Combo  {bestCombo}\n\nR - Run Again"; // 문구 형식이 잘못됐을 때 대비
            }

            target.verticalOverflow = VerticalWrapMode.Overflow; // 줄이 늘어도 잘리지 않게
            target.text = message;
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (heartIcons == null) return;

            bool useSprites = heartSprites != null && heartSprites.Length > 0;

            for (int i = 0; i < heartIcons.Length; i++)
            {
                Image icon = heartIcons[i];
                if (icon == null) continue;

                if (!useSprites)
                {
                    icon.color = i < current ? heartFullColor : heartEmptyColor;
                    continue;
                }

                icon.color = Color.white;
                bool full = i < current;
                bool justLost = !full && i < shownHearts;                      // 방금 잃은 심장
                bool justGained = full && shownHearts >= 0 && i >= shownHearts; // 방금 회복한 심장

                if (justLost)
                    StartDrain(i, icon, filling: false);
                else if (justGained)
                    StartDrain(i, icon, filling: true);
                else
                {
                    StopDrain(i);
                    icon.sprite = full ? FullSprite : EmptySprite;
                }
            }

            shownHearts = current;
        }

        private Sprite FullSprite => heartSprites[0];
        private Sprite EmptySprite => heartSprites[heartSprites.Length - 1];

        /// <summary>filling=false면 가득 → 빔, true면 빔 → 가득 순서로 중간 단계를 보여줍니다.</summary>
        private void StartDrain(int index, Image icon, bool filling)
        {
            StopDrain(index);

            // 중간 단계가 없거나 연출 시간이 0이면 바로 결과 모양으로
            if (!isActiveAndEnabled || heartDrainTime <= 0f || heartSprites.Length < 3)
            {
                icon.sprite = filling ? FullSprite : EmptySprite;
                return;
            }

            if (drainRoutines == null || drainRoutines.Length != heartIcons.Length)
                drainRoutines = new Coroutine[heartIcons.Length];
            if (iconBasePos == null || iconBasePos.Length != heartIcons.Length)
                iconBasePos = new Vector2[heartIcons.Length];
            iconBasePos[index] = icon.rectTransform.anchoredPosition; // StopDrain이 복원한 뒤라 원래 위치
            drainRoutines[index] = StartCoroutine(DrainRoutine(index, icon, filling));
        }

        private void StopDrain(int index)
        {
            if (drainRoutines == null || index >= drainRoutines.Length || drainRoutines[index] == null) return;
            StopCoroutine(drainRoutines[index]);
            drainRoutines[index] = null;
            SetPop(index, 1f); // 커진 채로 멈추지 않게 크기·위치 복원
        }

        /// <summary>아이콘을 중앙 기준으로 s배 키웁니다 (아이콘 피벗이 왼쪽 위여도 제자리에서 커지게 위치 보정).</summary>
        private void SetPop(int index, float s)
        {
            if (heartIcons == null || index >= heartIcons.Length || heartIcons[index] == null) return;
            if (iconBasePos == null || index >= iconBasePos.Length) return;

            RectTransform rt = heartIcons[index].rectTransform;
            Vector2 pivotToCenter = new Vector2((0.5f - rt.pivot.x) * rt.rect.width, (0.5f - rt.pivot.y) * rt.rect.height);
            rt.localScale = Vector3.one * s;
            rt.anchoredPosition = iconBasePos[index] + (1f - s) * pivotToCenter;
        }

        private IEnumerator DrainRoutine(int index, Image icon, bool filling)
        {
            // 첫 장(가득)과 마지막 장(빔) 사이의 중간 단계를 순서대로(회복이면 역순으로) 보여줍니다
            int middleCount = heartSprites.Length - 2;
            float stepTime = heartDrainTime / middleCount;
            for (int step = 0; step < middleCount; step++)
            {
                int s = filling ? middleCount - step : step + 1;
                icon.sprite = heartSprites[s];
                if (filling) SetPop(index, Mathf.Lerp(1f, healPopScale, (step + 1f) / middleCount));
                yield return new WaitForSecondsRealtime(stepTime); // 히트스톱(timeScale 0) 중에도 진행
            }
            icon.sprite = filling ? FullSprite : EmptySprite;

            // 회복: 커졌던 아이콘을 원래 크기로 되돌림
            if (filling)
            {
                float t = 0f, back = Mathf.Max(0.01f, heartDrainTime * 0.5f);
                while (t < back)
                {
                    t += Time.unscaledDeltaTime;
                    SetPop(index, Mathf.Lerp(healPopScale, 1f, t / back));
                    yield return null;
                }
            }
            SetPop(index, 1f);
            drainRoutines[index] = null;
        }

        private void HandleComboChanged(int combo)
        {
            if (comboText != null)
                comboText.text = combo > 0 ? $"{combo} COMBO" : string.Empty;
        }
    }
}
