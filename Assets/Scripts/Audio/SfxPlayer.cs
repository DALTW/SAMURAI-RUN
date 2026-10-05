using System;
using System.Collections.Generic;
using UnityEngine;

namespace SamuraiRunner.Audio
{
    /// <summary>
    /// 어디서든 효과음을 재생하는 짧은 입구: Sfx.Play(SfxId.Swing);
    /// </summary>
    public static class Sfx
    {
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            var player = SfxPlayer.Instance;
            if (player != null) player.Play(id, volume, pitch);
        }
    }

    /// <summary>
    /// 효과음 재생 담당. 게임이 시작되면 스스로 만들어지고 씬이 바뀌어도 유지됩니다
    /// (시작 화면의 START 소리가 게임 씬으로 넘어가도 끊기지 않게).
    /// 소리 파일·볼륨은 Resources/SoundSettings.asset (없으면 기본값 + Resources/Sfx)에서 읽습니다.
    /// 2D 효과음이라 위치와 상관없이 같은 크기로 들립니다. 히트스톱(시간 정지) 중에도 소리는 납니다.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        private const int VoiceCount = 16;

        private static SfxPlayer instance;

        /// <summary>플레이 중이면 없을 때 새로 만들어서 돌려줍니다.</summary>
        public static SfxPlayer Instance
        {
            get
            {
                if (instance == null && Application.isPlaying) Create();
                return instance;
            }
        }

        /// <summary>전체 볼륨 (0~1)</summary>
        public float MasterVolume { get => master; set => master = Mathf.Clamp01(value); }

        private float master = 0.8f;
        private readonly Dictionary<SfxId, SfxEntry> entries = new Dictionary<SfxId, SfxEntry>();
        private readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        private readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        private AudioSource[] voices;
        private int nextVoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance == null) Create(); // 첫 소리가 늦게 나지 않도록 시작할 때 미리 불러 둠
        }

        private static void Create()
        {
            var go = new GameObject("SfxPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SfxPlayer>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSounds();
            CreateVoices();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void LoadSounds()
        {
            var settings = Resources.Load<SoundSettings>("SoundSettings");
            if (settings != null) master = settings.masterVolume;

            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                SfxEntry entry = settings != null ? settings.Find(id) : null;
                if (entry == null) entry = SfxDefaults.Create(id);
                entries[id] = entry;

                AudioClip clip = entry.clip != null ? entry.clip : Resources.Load<AudioClip>("Sfx/" + SfxDefaults.FileName(id));
                if (clip == null)
                {
                    Debug.LogWarning($"[SfxPlayer] 효과음 파일이 없습니다: Resources/Sfx/{SfxDefaults.FileName(id)}");
                    continue;
                }
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                clips[id] = clip;
            }
        }

        private void CreateVoices()
        {
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 2D
                voices[i] = source;
            }
        }

        public void Play(SfxId id, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (voices == null || !clips.TryGetValue(id, out AudioClip clip)) return;
            SfxEntry entry = entries[id];

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < entry.cooldown) return;
            lastPlayed[id] = now;

            AudioSource voice = NextVoice();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(master * entry.volume * volumeScale);
            voice.pitch = Mathf.Max(0.1f, pitchScale * (1f + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter)));
            voice.Play();
        }

        /// <summary>쉬고 있는 채널을 우선 쓰고, 모두 바쁘면 가장 오래된 채널을 끊고 씀</summary>
        private AudioSource NextVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                int k = (nextVoice + i) % voices.Length;
                if (!voices[k].isPlaying)
                {
                    nextVoice = (k + 1) % voices.Length;
                    return voices[k];
                }
            }
            AudioSource oldest = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            return oldest;
        }
    }
}
