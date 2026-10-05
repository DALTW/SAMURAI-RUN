using UnityEngine;
using UnityEngine.SceneManagement;

namespace SamuraiRunner.Audio
{
    /// <summary>
    /// 어디서든 배경음악을 다루는 짧은 입구: Music.Duck(); Music.Restore();
    /// </summary>
    public static class Music
    {
        /// <summary>배경음악을 잠시 작게 줄입니다 (예: 쓰러졌을 때). 다음 씬을 불러오면 저절로 원래 크기로 돌아옵니다.</summary>
        public static void Duck(float level = 0.35f, float fadeTime = 1f)
        {
            var player = MusicPlayer.Instance;
            if (player != null) player.Duck(level, fadeTime);
        }

        /// <summary>줄였던 배경음악을 원래 크기로 되돌립니다.</summary>
        public static void Restore(float fadeTime = 0.6f)
        {
            var player = MusicPlayer.Instance;
            if (player != null) player.Duck(1f, fadeTime);
        }
    }

    /// <summary>
    /// 배경음악 재생 담당. 효과음 재생기처럼 게임이 시작되면 스스로 만들어지고 씬이 바뀌어도 이어서 재생됩니다
    /// (시작 화면에서 게임으로 넘어가도, R로 다시 달려도 음악이 끊기지 않음).
    /// 곡과 볼륨은 Resources/SoundSettings.asset 의 music · musicVolume 에서 읽습니다 (곡이 비어 있으면 Resources/Music/bgm_samurai).
    /// 곡은 이음새 없이 이어지도록 만들어져 있어 끝나면 처음부터 반복합니다.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        private const string FallbackClipPath = "Music/bgm_samurai";

        private static MusicPlayer instance;

        /// <summary>플레이 중이면 없을 때 새로 만들어서 돌려줍니다.</summary>
        public static MusicPlayer Instance
        {
            get
            {
                if (instance == null && Application.isPlaying) Create();
                return instance;
            }
        }

        [Tooltip("게임을 켰을 때 음악이 서서히 커지는 시간(초)")]
        [SerializeField] private float fadeInTime = 1.5f;
        [Tooltip("새 씬을 불러왔을 때 줄였던 음악이 원래 크기로 돌아오는 시간(초)")]
        [SerializeField] private float restoreTime = 0.6f;

        private AudioSource source;
        private SoundSettings settings;
        private float fadeIn;
        private float duck = 1f, duckFrom = 1f, duckTo = 1f, duckTime, duckElapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance == null) Create();
        }

        private static void Create()
        {
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
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

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D
            source.priority = 0;      // 효과음이 많이 겹쳐도 음악이 밀려나지 않게
            source.volume = 0f;

            settings = Resources.Load<SoundSettings>("SoundSettings");
            AudioClip clip = settings != null && settings.music != null ? settings.music : Resources.Load<AudioClip>(FallbackClipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[MusicPlayer] 배경음악 파일이 없습니다: Resources/{FallbackClipPath}");
                return;
            }
            source.clip = clip;
            source.Play();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (duckTo < 1f) Duck(1f, restoreTime); // 다시 달리기 시작하면 원래 크기로
        }

        /// <summary>음악 크기를 level(0~1)까지 time초 동안 바꿉니다. 1이면 원래 크기.</summary>
        public void Duck(float level, float time)
        {
            duckFrom = duck;
            duckTo = Mathf.Clamp01(level);
            duckTime = Mathf.Max(0f, time);
            duckElapsed = 0f;
            if (duckTime <= 0f) duck = duckTo;
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;

            float dt = Time.unscaledDeltaTime; // 히트스톱(시간 정지) 중에도 페이드는 진행
            fadeIn = fadeInTime > 0f ? Mathf.Min(1f, fadeIn + dt / fadeInTime) : 1f;
            if (duckElapsed < duckTime)
            {
                duckElapsed += dt;
                duck = Mathf.Lerp(duckFrom, duckTo, Mathf.Clamp01(duckElapsed / duckTime));
            }

            // 플레이 중에 SoundSettings 의 볼륨을 바꿔도 바로 반영
            float volume = settings != null ? settings.masterVolume * settings.musicVolume : 0.36f;
            source.volume = volume * fadeIn * duck;
        }
    }
}
