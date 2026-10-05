using System;
using System.Collections.Generic;
using UnityEngine;

namespace SamuraiRunner.Audio
{
    /// <summary>효과음 하나의 설정 (Inspector에서 조절).</summary>
    [Serializable]
    public class SfxEntry
    {
        public SfxId id;
        [Tooltip("재생할 소리. 비워두면 Resources/Sfx/(이름).wav 를 사용")]
        public AudioClip clip;
        [Tooltip("볼륨 (0~1, 전체 볼륨과 곱해짐)")]
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생할 때마다 음높이를 이만큼 무작위로 흔듦 (0.05 = ±5%) — 같은 소리가 반복돼도 덜 질리게")]
        [Range(0f, 0.3f)] public float pitchJitter;
        [Tooltip("같은 소리가 이 시간(초) 안에 또 나면 무시 (한 번에 겹쳐서 너무 커지는 것 방지)")]
        [Range(0f, 0.5f)] public float cooldown = 0.03f;
    }

    /// <summary>
    /// 소리 설정 에셋. Assets/Resources/SoundSettings.asset 에 두면 게임이 자동으로 읽습니다.
    /// 없거나 항목이 빠져 있으면 기본값(SfxDefaults)과 Resources/Sfx · Resources/Music 의 파일을 씁니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Samurai Runner/Sound Settings", fileName = "SoundSettings")]
    public class SoundSettings : ScriptableObject
    {
        [Tooltip("모든 효과음과 배경음악에 곱해지는 전체 볼륨")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;

        [Header("배경음악")]
        [Tooltip("반복 재생할 배경음악. 비워두면 Resources/Music/bgm_samurai 를 사용")]
        public AudioClip music;
        [Tooltip("배경음악 볼륨 (0~1, 전체 볼륨과 곱해짐) — 효과음이 묻히지 않게 낮게 둠")]
        [Range(0f, 1f)] public float musicVolume = 0.45f;

        [Header("효과음")]
        public List<SfxEntry> entries = new List<SfxEntry>();

        public SfxEntry Find(SfxId id)
        {
            foreach (var e in entries) if (e != null && e.id == id) return e;
            return null;
        }
    }
}
