using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SamuraiRunner.UI;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 스프라이트 참조 복구 도구.
    /// 에디터 밖에서 프리팹/씬 파일을 직접 수정한 뒤 스프라이트 ID가 맞지 않아 적·주먹밥이 투명하게 나올 때,
    /// Unity가 실제로 등록한 스프라이트를 이름 기준으로 다시 연결합니다.
    ///   - 궁수·닌자 프리팹: 시트 재임포트 + IDLE/ATTACK/DEATH 클립 + 대기 스프라이트
    ///   - HUD 주먹밥: hp_onigiri 5장 + 각 아이콘의 스프라이트
    /// 처음 한 번은 스크립트 로드 시 자동으로 실행되고(Library/SamuraiSpriteRelink.done 표시 파일로 1회 제한),
    /// 이후에는 Tools > Samurai Runner > 스프라이트 참조 다시 연결 메뉴로 언제든 다시 실행할 수 있습니다.
    /// 실행 결과는 Library/SamuraiSpriteRelink.report.txt 에 남습니다.
    /// </summary>
    public static class SamuraiSpriteRelinker
    {
        private const string MarkerPath = "Library/SamuraiSpriteRelink.done";
        private const string ReportPath = "Library/SamuraiSpriteRelink.report.txt";
        private const string HeartSheetPath = "Assets/Art/UI/hp_onigiri.png";

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            if (File.Exists(MarkerPath)) return;
            EditorApplication.update += WaitForIdleThenRun;
        }

        private static void WaitForIdleThenRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            EditorApplication.update -= WaitForIdleThenRun;
            if (File.Exists(MarkerPath)) return;
            Run(auto: true);
        }

        [MenuItem("Tools/Samurai Runner/스프라이트 참조 다시 연결 (궁수·닌자·주먹밥)")]
        public static void RunFromMenu() => Run(auto: false);

        private static void Run(bool auto)
        {
            var log = new StringBuilder();
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiSpriteRelinker auto={auto} unity={Application.unityVersion}");

            var scene = EditorSceneManager.GetActiveScene();
            bool sceneWasClean = !scene.isDirty;
            log.AppendLine($"scene: {scene.path} dirty(before)={scene.isDirty}");

            try
            {
                // ---------- 진단: 프리팹 참조 상태 ----------
                foreach (var name in new[] { "Archer", "Ninja" })
                    log.AppendLine("  " + DescribePrefab($"Assets/Prefabs/{name}.prefab"));

                // ---------- 궁수·닌자 ----------
                log.AppendLine("enemies:");
                bool ok = SamuraiEnemyAnimApplier.ApplyEnemiesOnly(log);
                foreach (var name in new[] { "Archer", "Ninja" })
                    log.AppendLine("  after: " + DescribePrefab($"Assets/Prefabs/{name}.prefab"));

                // ---------- 주먹밥 HUD ----------
                log.AppendLine("hud:");
                RelinkHud(log);

                // 우리가 바꾼 것만 저장 (원래 저장 안 된 변경이 있었다면 사용자가 직접 저장하도록 둠)
                if (sceneWasClean && scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine("scene saved");
                }
                else
                {
                    log.AppendLine("scene left dirty (had unsaved changes) — save it manually");
                }

                AssetDatabase.SaveAssets();
                log.AppendLine(ok ? "result: OK" : "result: PARTIAL (see errors above)");
                Debug.Log($"[SamuraiSpriteRelinker] 스프라이트 참조를 다시 연결했습니다. 리포트: {ReportPath}");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("EXCEPTION: " + ex);
                Debug.LogException(ex);
            }
            finally
            {
                File.WriteAllText(ReportPath, log.ToString());
                if (auto) File.WriteAllText(MarkerPath, System.DateTime.Now.ToString("o"));
            }
        }

        private static string DescribePrefab(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return $"{path}: MISSING";

            var sr = go.GetComponent<SpriteRenderer>();
            string sprite = sr == null ? "no SpriteRenderer" : (sr.sprite != null ? sr.sprite.name : "NULL");

            int clips = 0, frames = 0, nullFrames = 0;
            var anim = go.GetComponent<SamuraiRunner.Common.SpriteSheetAnimator>();
            if (anim != null)
            {
                var so = new SerializedObject(anim);
                var arr = so.FindProperty("clips");
                if (arr != null)
                {
                    clips = arr.arraySize;
                    for (int i = 0; i < arr.arraySize; i++)
                    {
                        var fr = arr.GetArrayElementAtIndex(i).FindPropertyRelative("frames");
                        if (fr == null) continue;
                        for (int j = 0; j < fr.arraySize; j++)
                        {
                            frames++;
                            if (fr.GetArrayElementAtIndex(j).objectReferenceValue == null) nullFrames++;
                        }
                    }
                }
            }
            return $"{path}: sprite={sprite}, animator={(anim != null ? "yes" : "no")}, clips={clips}, frames={frames}, nullFrames={nullFrames}";
        }

        private static void RelinkHud(StringBuilder log)
        {
            var hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud == null) { log.AppendLine("  GameHUD not in scene"); return; }

            int nullBefore = hud.heartSprites == null ? -1 : hud.heartSprites.Count(s => s == null);
            int iconNull = hud.heartIcons == null ? -1 : hud.heartIcons.Count(i => i != null && i.sprite == null);
            log.AppendLine($"  before: heartSprites={(hud.heartSprites?.Length ?? 0)} (null {nullBefore}), icons with null sprite={iconNull}");

            if (AssetImporter.GetAtPath(HeartSheetPath) is TextureImporter importer &&
                (importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Sprite[] onigiri = AssetDatabase.LoadAllAssetsAtPath(HeartSheetPath)
                .OfType<Sprite>()
                .OrderBy(s => { int i = s.name.LastIndexOf('_'); return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0; })
                .ToArray();
            log.AppendLine($"  {HeartSheetPath}: {onigiri.Length} sprites [{string.Join(", ", onigiri.Select(s => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string _, out long id) ? $"{s.name}={id}" : s.name))}]");
            if (onigiri.Length == 0) { log.AppendLine("  no onigiri sprites — skipped"); return; }

            Undo.RecordObject(hud, "Relink heart sprites");
            hud.heartSprites = onigiri;
            if (hud.heartIcons != null)
            {
                foreach (Image icon in hud.heartIcons)
                {
                    if (icon == null) continue;
                    Undo.RecordObject(icon, "Relink heart sprite");
                    icon.sprite = onigiri[0];
                    icon.color = Color.white;
                    icon.preserveAspect = true;
                    EditorUtility.SetDirty(icon);
                }
            }
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            log.AppendLine("  after: relinked heartSprites + icons");
        }
    }
}
