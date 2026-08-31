using UnityEngine;

namespace SamuraiRunner.CameraControl
{
    /// <summary>
    /// 카메라가 타깃(플레이어)을 부드럽게 따라갑니다.
    /// 러너 특성상 기본은 X축만 따라가고, Y는 시작 높이에 고정됩니다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("타깃")]
        [Tooltip("따라갈 대상 (플레이어)")]
        [SerializeField] private Transform target;

        [Header("위치")]
        [Tooltip("타깃 기준 카메라 오프셋 — X를 키우면 플레이어가 화면 왼쪽에 붙어 앞이 잘 보입니다")]
        [SerializeField] private Vector3 offset = new Vector3(2.5f, 0.8f, -10f);
        [Tooltip("체크하면 Y축도 따라갑니다 (기본은 시작 높이에 고정)")]
        [SerializeField] private bool followY = false;
        [Tooltip("따라가는 부드러움 (0이면 즉시, 클수록 느리게)")]
        [SerializeField] private float smoothTime = 0.12f;

        private Vector3 velocity;
        private float fixedY;

        private void Start()
        {
            fixedY = transform.position.y;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            if (!followY) desired.y = fixedY;
            desired.z = offset.z;

            transform.position = smoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
