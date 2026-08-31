using System;
using UnityEngine;

namespace SamuraiRunner.Common
{
    /// <summary>
    /// 하나의 애니메이션 클립 정보. Scene의 Inspector에서 프레임/속도/반복 여부를 직접 수정할 수 있습니다.
    /// </summary>
    [Serializable]
    public class SpriteAnimationClip
    {
        [Tooltip("클립 이름 (예: IDLE, RUN, JUMP, ATTACK, HURT)")]
        public string clipName;

        [Tooltip("순서대로 재생될 스프라이트 프레임들")]
        public Sprite[] frames;

        [Tooltip("초당 프레임 수 (높을수록 빠르게 재생)")]
        public float framesPerSecond = 12f;

        [Tooltip("체크하면 끝까지 재생한 뒤 처음부터 반복")]
        public bool loop = true;
    }

    /// <summary>
    /// Animator 없이 스프라이트 시트를 프레임 단위로 재생하는 범용 애니메이터.
    /// 플레이어뿐 아니라 나중에 추가될 적/이펙트에도 그대로 재사용할 수 있습니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteSheetAnimator : MonoBehaviour
    {
        [Header("클립 목록 (Scene에서 수정 가능)")]
        [SerializeField] private SpriteAnimationClip[] clips;

        /// <summary>루프가 아닌 클립이 끝까지 재생됐을 때 클립 이름과 함께 호출됩니다.</summary>
        public event Action<string> ClipFinished;

        public string CurrentClipName => current != null ? current.clipName : string.Empty;
        public bool IsFinished { get; private set; }

        private SpriteRenderer spriteRenderer;
        private SpriteAnimationClip current;
        private float timer;
        private int frameIndex;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>이름으로 클립을 재생합니다. 이미 같은 클립이면 restart=true일 때만 처음부터 다시 재생.</summary>
        public void Play(string clipName, bool restart = false)
        {
            if (current != null && current.clipName == clipName && !restart)
                return;

            current = FindClip(clipName);
            timer = 0f;
            frameIndex = 0;
            IsFinished = false;
            ApplyFrame();
        }

        /// <summary>에디터 셋업 스크립트가 클립 목록을 채울 때 사용합니다.</summary>
        public void SetClips(SpriteAnimationClip[] newClips) => clips = newClips;

        private void Update()
        {
            if (current == null || IsFinished || current.frames == null || current.frames.Length == 0)
                return;

            float frameTime = 1f / Mathf.Max(1f, current.framesPerSecond);
            timer += Time.deltaTime;

            while (timer >= frameTime)
            {
                timer -= frameTime;
                frameIndex++;

                if (frameIndex >= current.frames.Length)
                {
                    if (current.loop)
                    {
                        frameIndex = 0;
                    }
                    else
                    {
                        frameIndex = current.frames.Length - 1;
                        IsFinished = true;
                        ClipFinished?.Invoke(current.clipName);
                        break;
                    }
                }
            }

            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (current != null && current.frames != null && current.frames.Length > 0)
                spriteRenderer.sprite = current.frames[Mathf.Clamp(frameIndex, 0, current.frames.Length - 1)];
        }

        private SpriteAnimationClip FindClip(string clipName)
        {
            if (clips == null) return null;
            foreach (var clip in clips)
                if (clip != null && clip.clipName == clipName)
                    return clip;

            Debug.LogWarning($"[SpriteSheetAnimator] '{clipName}' 클립을 찾을 수 없습니다.", this);
            return null;
        }
    }
}
