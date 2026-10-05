using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SamuraiRunner.CameraControl;
using SamuraiRunner.Common;
using SamuraiRunner.Environment;
using SamuraiRunner.UI;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 10단계: 시작 화면(TitleScene) 만들기 에디터 스크립트.
    /// Tools > Samurai Runner > 10단계 - 시작 화면 만들기 를 실행하면:
    ///   1) 이전 단계(1~9)가 없으면 먼저 구성
    ///   2) Assets/Scenes/TitleScene.unity 를 새로 만듦
    ///      - 게임 씬의 카메라·2D 조명·대나무 배경을 복사, 끝없이 이어지는 바닥
    ///      - 달리기 동작으로 계속 달리는 사무라이(카메라가 따라가서 배경이 흘러감)
    ///      - "SAMURAI RUN" 픽셀 로고, 그 아래 START 버튼, 넘어갈 때 검게 덮는 화면
    ///   3) Build Settings 맨 앞에 TitleScene, 그 다음 게임 씬
    /// 처음 한 번은 스크립트 로드 시 TitleScene이 없으면 자동으로 만듭니다
    /// (Library/SamuraiTitleSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiTitleSetup.report.txt).
    /// Tools > Samurai Runner > Play는 시작 화면부터 를 켜 두면 어떤 씬에서 Play를 눌러도 시작 화면부터 시작합니다.
    /// </summary>
    public static class SamuraiTitleBuilder
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        private const string LogoPath = "Assets/Art/UI/title_logo.png";
        private const string StartLabelPath = "Assets/Art/UI/start_label.png";
        private const string ButtonPath = "Assets/Art/UI/ui_button.png";
        private const string RunSheetPath = "Assets/Assets/FREE_Samurai 2D Pixel Art v1.2/Sprites/RUN.png";
        private const string GroundTilePath = "Assets/Art/Backgrounds/ground_tile.png";
        private const string MarkerPath = "Library/SamuraiTitleSetup.done";
        private const string ReportPath = "Library/SamuraiTitleSetup.report.txt";
        private const string PlayFromTitlePref = "SamuraiRunner.PlayFromTitle";
        private const string PlayFromTitleMenu = "Tools/Samurai Runner/Play는 시작 화면부터";
        private const int LogoScale = 6;   // 로고 픽셀 1칸 = 화면 6px (1280x720 기준)
        private const int LabelScale = 3;  // 버튼 글자·테두리 픽셀 1칸 = 화면 3px
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

        [MenuItem("Tools/Samurai Runner/10단계 - 시작 화면 만들기")]
        public static void Apply()
        {
            SamuraiSpeedApplier.Apply();
            Run(auto: false, rebuild: true);
        }

        // ---------- Play는 시작 화면부터 (에디터 설정, 기본 켜짐) ----------
        [MenuItem(PlayFromTitleMenu)]
        private static void TogglePlayFromTitle()
        {
            EditorPrefs.SetBool(PlayFromTitlePref, !EditorPrefs.GetBool(PlayFromTitlePref, true));
            ApplyPlayModeStartScene();
        }

        [MenuItem(PlayFromTitleMenu, true)]
        private static bool TogglePlayFromTitleValidate()
        {
            Menu.SetChecked(PlayFromTitleMenu, EditorPrefs.GetBool(PlayFromTitlePref, true));
            return true;
        }

        private static void ApplyPlayModeStartScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
            EditorSceneManager.playModeStartScene = EditorPrefs.GetBool(PlayFromTitlePref, true) ? scene : null;
        }

        // ---------- 스크립트 로드 시 ----------
        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EditorApplication.delayCall += ApplyPlayModeStartScene;
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
            Run(auto: true, rebuild: false);
        }

        private static void Run(bool auto, bool rebuild)
        {
            var log = new StringBuilder();
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiTitleBuilder auto={auto} rebuild={rebuild}");
            bool ok = false;
            try
            {
                bool exists = File.Exists(TitleScenePath);
                if (!exists || rebuild) BuildTitleScene(log);
                else log.AppendLine("TitleScene already exists → verify only");

                EnsureBuildSettings(log);
                ApplyPlayModeStartScene();
                log.AppendLine($"Play starts from title: {EditorPrefs.GetBool(PlayFromTitlePref, true)} (menu: {PlayFromTitleMenu})");

                log.AppendLine("verify:");
                ok = Verify(log);
                log.AppendLine(ok ? "result: OK" : "result: FAILED (see above)");
                if (ok) Debug.Log($"[SamuraiTitleBuilder] 시작 화면 준비 완료: {TitleScenePath}. 리포트: {ReportPath}");
                else Debug.LogWarning($"[SamuraiTitleBuilder] 시작 화면을 확인하지 못했습니다. 리포트: {ReportPath}");
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

        // ---------- 만들기 ----------
        private static void BuildTitleScene(StringBuilder log)
        {
            if (SceneManager.GetSceneByPath(TitleScenePath).isLoaded)
            {
                log.AppendLine("  TitleScene is open in the editor → close it and run the menu again to rebuild");
                return;
            }

            ImportUiSprite(LogoPath, Vector4.zero);
            ImportUiSprite(StartLabelPath, Vector4.zero);
            ImportUiSprite(ButtonPath, new Vector4(4, 4, 4, 4));
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            var label = AssetDatabase.LoadAssetAtPath<Sprite>(StartLabelPath);
            var button = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPath);
            var groundTile = AssetDatabase.LoadAssetAtPath<Sprite>(GroundTilePath);
            Sprite[] run = AssetDatabase.LoadAllAssetsAtPath(RunSheetPath).OfType<Sprite>()
                .OrderBy(s => { int i = s.name.LastIndexOf('_'); return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0; })
                .ToArray();
            if (logo == null || label == null || button == null || run.Length == 0)
            {
                log.AppendLine($"  ERROR: sprites missing (logo={logo != null}, label={label != null}, button={button != null}, run={run.Length})");
                return;
            }

            // 카메라·조명·배경을 복사해 올 게임 씬 (열려 있지 않으면 잠깐 추가로 엶)
            Scene game = SceneManager.GetSceneByPath(GameScenePath);
            bool openedGame = false;
            if (!game.isLoaded)
            {
                game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                openedGame = true;
            }
            Scene previousActive = SceneManager.GetActiveScene();
            Scene title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(title);
            try
            {
                GameObject camSrc = FindRoot(game, "Main Camera");
                GameObject lightSrc = FindRoot(game, "Global Light 2D");
                GameObject bgSrc = FindRoot(game, "Background");
                GameObject groundSrc = FindRoot(game, "Ground");
                GameObject playerSrc = FindRoot(game, "Player");

                GameObject cam = Copy(camSrc, log);
                Copy(lightSrc, log);
                Copy(bgSrc, log);

                // 바닥: 카메라를 따라 타일 단위로 되감기는 긴 바닥 (월드에 고정된 것처럼 보임)
                var groundSR = groundSrc != null ? groundSrc.GetComponent<SpriteRenderer>() : null;
                float groundCenterY = groundSrc != null ? groundSrc.transform.position.y : -2.5f;
                float thickness = groundSR != null ? groundSR.size.y : 2f;
                var ground = new GameObject("Ground");
                ground.transform.position = new Vector3(0f, groundCenterY, 0f);
                var gsr = ground.AddComponent<SpriteRenderer>();
                gsr.sprite = groundTile;
                if (groundSR != null) { gsr.sharedMaterial = groundSR.sharedMaterial; gsr.sortingLayerID = groundSR.sortingLayerID; gsr.sortingOrder = groundSR.sortingOrder; }
                gsr.drawMode = SpriteDrawMode.Tiled;
                gsr.size = new Vector2(24f, thickness);
                var gpl = ground.AddComponent<ParallaxLayer>();
                gpl.parallaxFactor = 1f;
                gpl.lockedY = groundCenterY;

                // 달리는 사무라이: 발이 바닥 윗면에 닿는 높이 (게임 플레이어의 콜라이더 기준)
                float groundTop = groundCenterY + thickness * 0.5f;
                float standY = -0.4375f;
                var playerCol = playerSrc != null ? playerSrc.GetComponent<BoxCollider2D>() : null;
                if (playerCol != null) standY = groundTop - (playerCol.offset.y - playerCol.size.y * 0.5f);
                var samurai = new GameObject("Samurai");
                samurai.transform.position = new Vector3(0f, standY, 0f);
                var ssr = samurai.AddComponent<SpriteRenderer>();
                ssr.sprite = run[0];
                var playerSR = playerSrc != null ? playerSrc.GetComponent<SpriteRenderer>() : null;
                if (playerSR != null) { ssr.sharedMaterial = playerSR.sharedMaterial; ssr.sortingLayerID = playerSR.sortingLayerID; ssr.sortingOrder = playerSR.sortingOrder; }
                var anim = samurai.AddComponent<SpriteSheetAnimator>();
                anim.SetClips(new[] { new SpriteAnimationClip { clipName = "RUN", frames = run, framesPerSecond = 14f, loop = true } });
                samurai.AddComponent<TitleRunner>();

                // 카메라가 사무라이를 따라감 (사무라이는 화면 왼쪽 아래, 위쪽 가운데에 제목·버튼)
                if (cam != null && cam.TryGetComponent<CameraFollow>(out var follow))
                {
                    var so = new SerializedObject(follow);
                    so.FindProperty("target").objectReferenceValue = samurai.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                Button startButton = BuildUI(logo, label, button, out Image fade, out GameObject canvasGO);

                var esGO = new GameObject("EventSystem");
                var es = esGO.AddComponent<EventSystem>();
                var module = esGO.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions(); // 마우스 클릭·Enter·패드 입력
                es.firstSelectedGameObject = startButton.gameObject;

                var screen = canvasGO.AddComponent<TitleScreen>();
                screen.startButton = startButton;
                screen.fadeImage = fade;

                EditorSceneManager.SaveScene(title, TitleScenePath);
                log.AppendLine($"  {TitleScenePath}: built (run frames={run.Length}, stand y={standY:0.###}, ground y={groundCenterY})");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(title, true);
                if (openedGame) EditorSceneManager.CloseScene(game, true);
            }
        }

        private static Button BuildUI(Sprite logo, Sprite label, Sprite button, out Image fade, out GameObject canvasGO)
        {
            canvasGO = new GameObject("TitleUI", typeof(RectTransform)) { layer = 5 };
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // 제목 로고
            var titleRT = CreateUI("Title", canvasGO.transform, new Vector2(0f, 165f), logo.rect.size * LogoScale);
            var titleImg = titleRT.gameObject.AddComponent<Image>();
            titleImg.sprite = logo;
            titleImg.preserveAspect = true;
            titleImg.raycastTarget = false;

            // 제목 아래 START 버튼 (9-slice, 픽셀 3배)
            var buttonRT = CreateUI("StartButton", canvasGO.transform, new Vector2(0f, -15f), new Vector2(97f, 32f) * LabelScale);
            var buttonImg = buttonRT.gameObject.AddComponent<Image>();
            buttonImg.sprite = button;
            buttonImg.type = Image.Type.Sliced;
            buttonImg.pixelsPerUnitMultiplier = scaler.referencePixelsPerUnit / button.pixelsPerUnit / LabelScale;
            var startButton = buttonRT.gameObject.AddComponent<Button>();
            startButton.targetGraphic = buttonImg;
            ColorBlock colors = startButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.72f);
            colors.selectedColor = new Color(1f, 0.92f, 0.72f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.55f);
            colors.fadeDuration = 0.08f;
            startButton.colors = colors;

            var labelRT = CreateUI("Label", buttonRT, Vector2.zero, label.rect.size * LabelScale);
            var labelImg = labelRT.gameObject.AddComponent<Image>();
            labelImg.sprite = label;
            labelImg.preserveAspect = true;
            labelImg.raycastTarget = false;

            // 넘어갈 때 화면을 덮는 검은 판 (맨 위)
            var fadeRT = CreateUI("Fade", canvasGO.transform, Vector2.zero, Vector2.zero);
            fadeRT.anchorMin = Vector2.zero;
            fadeRT.anchorMax = Vector2.one;
            fadeRT.sizeDelta = Vector2.zero;
            fade = fadeRT.gameObject.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;

            return startButton;
        }

        private static RectTransform CreateUI(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        private static GameObject Copy(GameObject source, StringBuilder log)
        {
            if (source == null) { log.AppendLine("  (copy skipped: source missing)"); return null; }
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            return copy;
        }

        private static GameObject FindRoot(Scene scene, string name)
            => scene.IsValid() ? scene.GetRootGameObjects().FirstOrDefault(g => g.name == name) : null;

        private static void ImportUiSprite(string path, Vector4 border)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // 9-slice가 깨지지 않게
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void EnsureBuildSettings(StringBuilder log)
        {
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == TitleScenePath);
            list.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));
            if (!list.Any(s => s.path == GameScenePath)) list.Add(new EditorBuildSettingsScene(GameScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            log.AppendLine("Build Settings: " + string.Join(" → ", list.Select(s => $"{Path.GetFileNameWithoutExtension(s.path)}({(s.enabled ? "on" : "off")})")));
        }

        // ---------- 검증 (열어 보기만 하고 저장하지 않음) ----------
        private static bool Verify(StringBuilder log)
        {
            if (!File.Exists(TitleScenePath)) { log.AppendLine("  TitleScene: MISSING"); return false; }

            Scene title = SceneManager.GetSceneByPath(TitleScenePath);
            bool opened = false;
            if (!title.isLoaded)
            {
                title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                bool ok = true;
                var roots = title.GetRootGameObjects();
                var screen = roots.Select(r => r.GetComponentInChildren<TitleScreen>(true)).FirstOrDefault(c => c != null);
                var module = roots.Select(r => r.GetComponentInChildren<InputSystemUIInputModule>(true)).FirstOrDefault(c => c != null);
                var runner = roots.Select(r => r.GetComponentInChildren<TitleRunner>(true)).FirstOrDefault(c => c != null);
                var follow = roots.Select(r => r.GetComponentInChildren<CameraFollow>(true)).FirstOrDefault(c => c != null);
                var light = roots.FirstOrDefault(r => r.name == "Global Light 2D");
                var bg = roots.FirstOrDefault(r => r.name == "Background");

                bool buttonOk = screen != null && screen.startButton != null;
                log.AppendLine($"  TitleScreen={(screen != null)}, startButton={buttonOk}, fade={(screen != null && screen.fadeImage != null)}");
                log.AppendLine($"  EventSystem input module={(module != null)}, actions={(module != null && module.actionsAsset != null)}");
                int frames = 0;
                if (runner != null && runner.TryGetComponent<SpriteSheetAnimator>(out var anim))
                {
                    var clips = new SerializedObject(anim).FindProperty("clips");
                    if (clips != null && clips.arraySize > 0) frames = clips.GetArrayElementAtIndex(0).FindPropertyRelative("frames").arraySize;
                }
                Object target = follow != null ? new SerializedObject(follow).FindProperty("target").objectReferenceValue : null;
                log.AppendLine($"  Samurai runner={(runner != null)}, RUN frames={frames}, camera follows samurai={(runner != null && target == runner.transform)}");
                log.AppendLine($"  copied: Global Light 2D={(light != null)}, Background={(bg != null)} (layers {(bg != null ? bg.GetComponentsInChildren<ParallaxLayer>(true).Length : 0)})");
                if (buttonOk)
                {
                    var img = screen.startButton.GetComponent<Image>();
                    log.AppendLine($"  START button sprite={(img != null && img.sprite != null ? img.sprite.name : "NULL")}, border={(img != null && img.sprite != null ? img.sprite.border.ToString() : "-")}");
                }
                if (!buttonOk || module == null || module.actionsAsset == null || runner == null || frames == 0 || target != (runner != null ? runner.transform : null))
                    ok = false;
                return ok;
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(title, true);
            }
        }
    }
}
