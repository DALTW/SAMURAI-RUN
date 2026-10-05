using UnityEngine;
using SamuraiRunner.Common;

namespace SamuraiRunner.UI
{
    /// <summary>
    /// 시작 화면 배경용 사무라이: 달리기 동작을 반복하며 오른쪽으로 계속 달립니다.
    /// 카메라(CameraFollow)가 따라가므로 배경(대나무·바닥)이 흘러가 보입니다. 물리·입력은 쓰지 않습니다.
    /// </summary>
    [RequireComponent(typeof(SpriteSheetAnimator))]
    public class TitleRunner : MonoBehaviour
    {
        [Tooltip("달리는 속도 (유닛/초) — 배경이 흘러가는 속도")]
        [SerializeField] private float speed = 5f;
        [Tooltip("재생할 달리기 클립 이름 (SpriteSheetAnimator의 클립 이름)")]
        [SerializeField] private string runClip = "RUN";

        private SpriteSheetAnimator animator;

        private void Awake()
        {
            animator = GetComponent<SpriteSheetAnimator>();
        }

        private void Start()
        {
            animator.Play(runClip);
        }

        private void Update()
        {
            transform.position += Vector3.right * (speed * Time.deltaTime);
        }
    }
}
