using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using SamuraiRunner.Audio;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 11단계: 효과음 적용 에디터 스크립트.
    /// Tools > Samurai Runner > 11단계 - 효과음 적용 을 실행하면:
    ///   1) 이전 단계(1~10)가 없으면 먼저 구성
    ///   2) Assets/Resources/Sfx 의 효과음(Tools/SfxGen 으로 합성)을 짧은 효과음용 설정으로 가져옴
    ///      (모노, 불러올 때 바로 풀어 두기, 무압축 PCM)
    ///   3) Assets/Resources/SoundSettings.asset 을 만들거나 빠진 항목만 채움 (이미 고친 볼륨은 유지)
    /// 처음 한 번은 스크립트 로드 시 자동으로 실행됩니다
    /// (Library/SamuraiSoundSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiSoundSetup.report.txt).
    /// 효과음은 씬 설정 없이 SfxPlayer가 게임 시작 때 스스로 만들어져 재생합니다.
    /// </summary>
    public static class SamuraiSoundApplier
    {
        private const string SfxFolder = "Assets/Resources/Sfx";
        private const string SettingsPath = "Assets/Resources/SoundSettings.asset";
        private const string MarkerPath = "Library/SamuraiSoundSetup.done";
        private const string ReportPath = "Library/SamuraiSoundSetup.report.txt";
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

        [MenuItem("Tools/Samurai Runner/11단계 - 효과음 적용")]
        public static void Apply()
        {
            SamuraiTitleBuilder.Apply();
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
            log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiSoundApplier auto={auto}");
            bool ok = true;
            try
            {
                // 1) 가져오기 설정
                int imported = 0;
                foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                {
                    string path = $"{SfxFolder}/{SfxDefaults.FileName(id)}.wav";
                    if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                    var s = importer.defaultSampleSettings;
                    bool changed = !importer.forceToMono || s.loadType != AudioClipLoadType.DecompressOnLoad || s.compressionFormat != AudioCompressionFormat.PCM;
                    if (!changed) continue;
                    importer.forceToMono = true;
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.PCM;
                    s.preloadAudioData = true;
                    importer.defaultSampleSettings = s;
                    importer.SaveAndReimport();
                    imported++;
                }
                log.AppendLine($"import settings updated: {imported} clip(s)");

                // 2) SoundSettings 에셋
                var settings = AssetDatabase.LoadAssetAtPath<SoundSettings>(SettingsPath);
                bool created = false;
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<SoundSettings>();
                    AssetDatabase.CreateAsset(settings, SettingsPath);
                    created = true;
                }
                int added = 0, linked = 0;
                foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                {
                    var entry = settings.Find(id);
                    if (entry == null) { entry = SfxDefaults.Create(id); settings.entries.Add(entry); added++; }
                    if (entry.clip == null)
                    {
                        entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{SfxDefaults.FileName(id)}.wav");
                        if (entry.clip != null) linked++;
                    }
                }
                settings.entries = settings.entries.OrderBy(e => (int)e.id).ToList();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                log.AppendLine($"{SettingsPath}: {(created ? "created" : "updated")}, entries added={added}, clips linked={linked}, master={settings.masterVolume}");

                // 3) 검증
                log.AppendLine("verify:");
                foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                {
                    var entry = settings.Find(id);
                    var clip = entry != null ? entry.clip : null;
                    bool clipOk = clip != null && clip.length > 0f;
                    if (!clipOk) ok = false;
                    log.AppendLine($"  {id,-14} {(clipOk ? $"{clip.name}.wav {clip.length:0.00}s {clip.channels}ch" : "MISSING"),-28} vol={entry?.volume:0.00} jitter={entry?.pitchJitter:0.00}");
                }
                var runtimeSettings = Resources.Load<SoundSettings>("SoundSettings");
                log.AppendLine($"  Resources.Load<SoundSettings>(\"SoundSettings\") → {(runtimeSettings != null ? "found" : "NOT FOUND")}");
                if (runtimeSettings == null) ok = false;

                log.AppendLine(ok ? "result: OK" : "result: FAILED (see above)");
                if (ok) Debug.Log($"[SamuraiSoundApplier] 효과음 {Enum.GetValues(typeof(SfxId)).Length}개 준비 완료. 리포트: {ReportPath}");
                else Debug.LogWarning($"[SamuraiSoundApplier] 효과음 설정을 확인하지 못했습니다. 리포트: {ReportPath}");
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
