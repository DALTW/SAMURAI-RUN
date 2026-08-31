using UnityEngine;

namespace SamuraiRunner.Environment
{
    /// <summary>
    /// 배경 한 겹의 패럴랙스 스크롤 담당. 가로로 반복되는(타일) 스프라이트와 함께 사용합니다.
    /// 계수와 높이는 Scene의 Inspector에서 조절합니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("0 = 카메라에 완전히 고정(하늘), 1 = 월드에 고정. 멀수록 작게")]
        [Range(0f, 1f)] public float parallaxFactor = 0.3f;

        [Tooltip("레이어가 유지할 월드 Y")]
        public float lockedY = 0f;

        [Tooltip("비워두면 Main Camera 사용")]
        public Camera targetCamera;

        private SpriteRenderer sr;
        private float tileWidth;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (targetCamera == null) targetCamera = Camera.main;
            tileWidth = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            float camX = targetCamera.transform.position.x;
            // 카메라가 이동한 만큼의 일부만 스크롤하고, 타일 폭 단위로 되감아 무한 반복
            float offset = parallaxFactor > 0f ? Mathf.Repeat(camX * parallaxFactor, tileWidth) : 0f;
            transform.position = new Vector3(camX - offset, lockedY, transform.position.z);
        }
    }
}
