using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.CameraControl;
using SamuraiRunner.Common;
using SamuraiRunner.Player;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 씬을 구성하는 에디터 전용 스크립트.
    /// Tools > Samurai Runner > 씬 자동 구성 을 실행하면:
    ///   1) 스프라이트 임포트 설정(픽셀아트용 Point 필터, PPU 32)
    ///   2) Ground 레이어 생성
    ///   3) 플레이어(컴포넌트 + 애니메이션 클립 연결), 바닥, 카메라 배치
    /// 실행 후 모든 오브젝트/수치는 Scene에서 자유롭게 수정하면 됩니다.
    /// </summary>
    public static class SamuraiSceneBuilder
    {
        private const string SpriteFolder = "Assets/Assets/FREE_Samurai 2D Pixel Art v1.2/Sprites";
        private const string PlaceholderFolder = "Assets/Assets/Placeholders";
        private const string GroundLayerName = "Ground";
        private const int PixelsPerUnit = 32;

        [MenuItem("Tools/Samurai Runner/씬 자동 구성")]
        public static void BuildScene()
        {
            ConfigureSpriteImporters();
            SamuraiSpritePivotFixer.FixPivots(); // 프레임별 crop 차이를 없애는 피벗 통일
            EnsureLayerExists(GroundLayerName);
            Sprite square = CreateSquareSprite();

            Sprite[] idle = LoadFrames("IDLE.png");
            Sprite[] run = LoadFrames("RUN.png");
            Sprite[] attack = LoadFrames("ATTACK 1.png");
            Sprite[] hurt = LoadFrames("HURT.png");

            if (idle.Length == 0 || run.Length == 0)
            {
                Debug.LogError($"[SamuraiSceneBuilder] 스프라이트를 찾을 수 없습니다: {SpriteFolder}");
                return;
            }

            GameObject player = BuildPlayer(idle, run, attack, hurt);
            BuildGround(square);
            BuildCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = player;
            Debug.Log("[SamuraiSceneBuilder] 씬 구성 완료! Play를 누르세요. 조작: Z = 점프, X = 베기");
        }

        // ---------- 스프라이트 임포트 설정 ----------
        private static void ConfigureSpriteImporters()
        {
            string[] files = { "IDLE.png", "RUN.png", "ATTACK 1.png", "HURT.png" };
            foreach (string file in files)
            {
                string path = $"{SpriteFolder}/{file}";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static Sprite[] LoadFrames(string file)
        {
            return AssetDatabase.LoadAllAssetsAtPath($"{SpriteFolder}/{file}")
                .OfType<Sprite>()
                .OrderBy(s =>
                {
                    int i = s.name.LastIndexOf('_');
                    return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0;
                })
                .ToArray();
        }

        // ---------- 플레이어 ----------
        private static GameObject BuildPlayer(Sprite[] idle, Sprite[] run, Sprite[] attack, Sprite[] hurt)
        {
            GameObject player = GameObject.Find("Player") ?? new GameObject("Player");

            var sr = GetOrAdd<SpriteRenderer>(player);
            sr.sprite = idle[0];

            var rb = GetOrAdd<Rigidbody2D>(player);
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = GetOrAdd<BoxCollider2D>(player);

            var anim = GetOrAdd<SpriteSheetAnimator>(player);
            Sprite jumpFrame = run.Length > 11 ? run[11] : run[run.Length / 2]; // 다리를 접은 도약 프레임
            anim.SetClips(new[]
            {
                new SpriteAnimationClip { clipName = "IDLE",   frames = idle,                    framesPerSecond = 10f, loop = true  },
                new SpriteAnimationClip { clipName = "RUN",    frames = run,                     framesPerSecond = 14f, loop = true  },
                new SpriteAnimationClip { clipName = "JUMP",   frames = new[] { jumpFrame },     framesPerSecond = 1f,  loop = false },
                new SpriteAnimationClip { clipName = "ATTACK", frames = attack,                  framesPerSecond = 18f, loop = false },
                new SpriteAnimationClip { clipName = "HURT",   frames = hurt,                    framesPerSecond = 12f, loop = false },
            });
            EditorUtility.SetDirty(anim);

            GetOrAdd<PlayerInputReader>(player);

            var motor = GetOrAdd<PlayerMotor>(player);
            var so = new SerializedObject(motor);
            so.FindProperty("groundLayer").intValue = 1 << LayerMask.NameToLayer(GroundLayerName);
            so.ApplyModifiedProperties();

            GetOrAdd<PlayerAttack>(player);
            GetOrAdd<PlayerAnimator>(player);

            // 스프라이트 실제 크기 기준으로 위치·콜라이더·판정 재계산
            SamuraiPlayerGeometryFixer.ApplyGeometry(player, idle[0]);

            return player;
        }

        // ---------- 바닥 ----------
        private static void BuildGround(Sprite square)
        {
            GameObject ground = GameObject.Find("Ground") ?? new GameObject("Ground");
            ground.layer = LayerMask.NameToLayer(GroundLayerName);
            ground.transform.position = new Vector3(150f, -2f, 0f);
            ground.transform.localScale = new Vector3(500f, 1f, 1f); // 우선 넉넉하게 긴 바닥

            var sr = GetOrAdd<SpriteRenderer>(ground);
            sr.sprite = square;
            sr.color = new Color(0.18f, 0.14f, 0.12f); // 어두운 흙색 (임시)

            GetOrAdd<BoxCollider2D>(ground);
        }

        // ---------- 카메라 ----------
        private static void BuildCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.14f); // 새벽하늘 느낌 (임시)
            cam.transform.position = target.position + new Vector3(2.5f, 0.8f, -10f);

            var follow = GetOrAdd<CameraFollow>(cam.gameObject);
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedProperties();
        }

        // ---------- 유틸 ----------
        internal static Sprite CreateSquareSprite()
        {
            Directory.CreateDirectory(PlaceholderFolder);
            string path = $"{PlaceholderFolder}/square.png";

            if (!File.Exists(path))
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                tex.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 4; // 4px 텍스처 = 1유닛짜리 사각형
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static void EnsureLayerExists(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) != -1) return;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
            Debug.LogError("[SamuraiSceneBuilder] 비어있는 레이어 슬롯이 없습니다.");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out T c) ? c : go.AddComponent<T>();
    }
}
