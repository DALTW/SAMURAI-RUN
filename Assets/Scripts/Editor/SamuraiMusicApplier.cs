using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SamuraiRunner.Audio;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 12단계: 배경음악 적용 에디터 스크립트.
    /// Tools > Samurai Runner > 12단계 - 배경음악 적용 을 실행하면:
    ///   1) 이전 단계(1~11)가 없으면 먼저 구성
    ///   2) Assets/Resources/Music/bgm_samurai.wav (Tools/BgmGen 으로 작곡·합성)를 음악용 설정으로 가져옴
    ///      (스테레오 유지, Vorbis 압축, 압축된 채로 메모리에 두고 재생하면서 풀기, 백그라운드 로드)
    ///   3) SoundSettings.asset 의 music 칸에 연결 (이미 다른 곡을 넣어 두었으면 그대로 둠)
    /// 처음 한 번은 스크립트 로드 시 자동으로 실행됩니다
    /// (Library/SamuraiMusicSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiMusicSetup.report.txt).
    /// 배경음악은 씬 설정 없이 MusicPlayer가 게임 시작 때 스스로 만들어져 재생합니다.
    /// </summary>
    public static class SamuraiMusicApplier
    {
        private const string MusicPath = "Assets/Resources/Music/bgm_samurai.wav";
        private const string SettingsPath = "Assets/Resources/SoundSettings.asset";
        private const string MarkerPath = "Library/SamuraiMusicSetup.done";
        private const string ReportPath = "Library/SamuraiMusicSetup.report.txt";
        private const float VorbisQuality = 0.7f;
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

        [MenuItem("Tools/Samurai Runner/12단계 - 배경음악 적용")]
        public static void Apply()
        {
            SamuraiSoundApplier.Apply();
            Run(auto: false);
        }

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            if (File.Exists(MarkerPath)) return;
            idleSince = -1;
            EditorApplication.update += WaitForIdleThenRun;
        }

        private static void WaitForIdleThenRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                idleSince = -1;
                return;
            }
            if (idleSince < 0) { idleSince = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - idleSince < SettleSeconds) return;

            EditorApplication.update -= WaitForIdleThenRun;
            if (File.Exists(MarkerPath)) return;
            Run(auto: true);
        }

        private static void Run(bool auto)
        {
            var log = new StringBuilder();
            log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiMusicApplier auto={auto}");
            bool ok = true;
            try
            {
                // 1) 가져오기 설정
                if (AssetImporter.GetAtPath(MusicPath) is not AudioImporter importer)
                {
                    log.AppendLine($"{MusicPath}: NOT FOUND (Tools/BgmGen 으로 만들어야 함)");
                    ok = false;
                }
                else
                {
                    var s = importer.defaultSampleSettings;
                    bool changed = importer.forceToMono || !importer.loadInBackground ||
                                   s.loadType != AudioClipLoadType.CompressedInMemory ||
                                   s.compressionFormat != AudioCompressionFormat.Vorbis ||
                                   Mathf.Abs(s.quality - VorbisQuality) > 0.01f;
                    if (changed)
                    {
                        importer.forceToMono = false;
                        importer.loadInBackground = true;
                        s.loadType = AudioClipLoadType.CompressedInMemory;
                        s.compressionFormat = AudioCompressionFormat.Vorbis;
                        s.quality = VorbisQuality;
                        s.preloadAudioData = true;
                        importer.defaultSampleSettings = s;
                        importer.SaveAndReimport();
                    }
                    log.AppendLine($"{MusicPath}: import settings {(changed ? "updated" : "already set")} (stereo, Vorbis q={VorbisQuality}, CompressedInMemory, loadInBackground)");
                }

                // 2) SoundSettings 에 연결 (SerializedObject 로 읽고 써야 가져온 직후에도 정확함)
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
                var settings = AssetDatabase.LoadAssetAtPath<SoundSettings>(SettingsPath);
                if (settings == null)
                {
                    log.AppendLine($"{SettingsPath}: NOT FOUND (11단계를 먼저 실행해야 함)");
                    ok = false;
                }
                else
                {
                    var so = new SerializedObject(settings);
                    var musicProp = so.FindProperty("music");
                    var volumeProp = so.FindProperty("musicVolume");
                    if (musicProp == null || volumeProp == null)
                    {
                        log.AppendLine("SoundSettings has no music/musicVolume field yet (scripts not reloaded?)");
                        ok = false;
                    }
                    else
                    {
                        bool linked = false;
                        if (musicProp.objectReferenceValue == null && clip != null)
                        {
                            musicProp.objectReferenceValue = clip;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(settings);
                            AssetDatabase.SaveAssets();
                            linked = true;
                        }
                        var current = musicProp.objectReferenceValue as AudioClip;
                        log.AppendLine($"{SettingsPath}: music={(current != null ? current.name : "NONE")} ({(linked ? "linked now" : "kept")}), musicVolume={volumeProp.floatValue:0.00}, master={settings.masterVolume:0.00}");
                        if (current == null) ok = false;
                    }
                }

                // 3) 검증
                log.AppendLine("verify:");
                bool clipOk = clip != null && clip.length > 60f && clip.channels == 2;
                log.AppendLine($"  clip: {(clip != null ? $"{clip.name} {clip.length:0.00}s {clip.channels}ch {clip.frequency}Hz loadType={clip.loadType}" : "MISSING")} → {(clipOk ? "OK" : "FAILED")}");
                if (!clipOk) ok = false;
                var runtimeSettings = Resources.Load<SoundSettings>("SoundSettings");
                log.AppendLine($"  Resources.Load<SoundSettings>(\"SoundSettings\") → {(runtimeSettings != null ? "found" : "NOT FOUND")}");
                if (runtimeSettings == null) ok = false;
                var fallback = Resources.Load<AudioClip>("Music/bgm_samurai");
                log.AppendLine($"  Resources.Load<AudioClip>(\"Music/bgm_samurai\") → {(fallback != null ? "found" : "NOT FOUND")}");
                if (fallback == null) ok = false;

                log.AppendLine(ok ? "result: OK" : "result: FAILED (see above)");
                if (ok) Debug.Log($"[SamuraiMusicApplier] 배경음악 준비 완료 ({(clip != null ? clip.length : 0f):0.0}초, 반복 재생). 리포트: {ReportPath}");
                else Debug.LogWarning($"[SamuraiMusicApplier] 배경음악 설정을 확인하지 못했습니다. 리포트: {ReportPath}");
            }
            catch (Exception ex)
            {
                log.AppendLine("EXCEPTION: " + ex);
                Debug.LogException(ex);
            }
            finally
            {
                File.WriteAllText(ReportPath, log.ToString());
                if (auto) File.WriteAllText(MarkerPath, DateTime.Now.ToString("o"));
            }
        }
    }
}
