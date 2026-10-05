using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.Common;
using SamuraiRunner.Enemies;
using SamuraiRunner.Spawning;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 5단계: 궁수·닌자 애니메이션 적용 에디터 스크립트.
    /// Tools > Samurai Runner > 5단계 - 궁수·닌자 애니메이션 적용 을 실행하면:
    ///   1) 이전 단계(1~4)가 없으면 먼저 구성
    ///   2) Assets/Art/Enemies의 대기·공격·사망 시트(64x48 셀)를 픽셀아트 설정 + 발밑 피벗으로 임포트
    ///   3) 궁수·닌자 프리팹에 SpriteSheetAnimator 클립(IDLE/ATTACK/DEATH) 연결,
    ///      발사 프레임·발사 위치·몸통 콜라이더를 새 그림에 맞게 설정
    ///   4) 스포너의 스폰 높이를 발 기준(바닥 윗면)으로 갱신
    /// 이후 프레임 속도·발사 프레임·발사 위치는 프리팹에서 자유롭게 수정하면 됩니다.
    /// </summary>
    public static class SamuraiEnemyAnimApplier
    {
        internal const string ArtFolder = "Assets/Art/Enemies";
        private const int PixelsPerUnit = 32;
        private const float GroundTopY = -1.5f;
        private const int CellWidth = 64, CellHeight = 48;

        // 시트 셀 64x48, 캐릭터 몸통 중심이 x=43px(활·팔은 왼쪽으로 뻗음) → 피벗을 몸통 발밑에 둡니다
        private static readonly Vector2 FeetPivot = new Vector2(43f / 64f, 0f);

        internal struct EnemyArt
        {
            public string prefab, file;
            public int releaseFrame;                 // ATTACK 클립에서 투사체가 나가는 프레임
            public float idleFps, attackFps, deathFps;
            public Vector2 firePoint;                // 발사 위치 (피벗 기준, 유닛)
            public Vector2 colliderSize, colliderOffset; // 몸통(머리~발) 기준. 모자·활은 제외
        }

        internal static readonly EnemyArt[] Enemies =
        {
            new EnemyArt
            {
                prefab = "Archer", file = "archer", releaseFrame = 4, idleFps = 6f, attackFps = 10f, deathFps = 8f,
                firePoint = new Vector2(-0.78f, 0.625f),
                colliderSize = new Vector2(0.375f, 0.90625f), colliderOffset = new Vector2(0f, 0.453125f),
            },
            new EnemyArt
            {
                prefab = "Ninja", file = "ninja", releaseFrame = 3, idleFps = 6f, attackFps = 10f, deathFps = 8f,
                firePoint = new Vector2(-0.59f, 0.625f),
                colliderSize = new Vector2(0.3125f, 0.9375f), colliderOffset = new Vector2(0f, 0.46875f),
            },
        };

        [MenuItem("Tools/Samurai Runner/5단계 - 궁수·닌자 애니메이션 적용")]
        public static void Apply()
        {
            SamuraiVisualsApplier.Apply();
            ApplyEnemiesOnly(null);
            Debug.Log("[SamuraiEnemyAnimApplier] 궁수·닌자 애니메이션 적용 완료! (대기·공격·사망)");
        }

        /// <summary>
        /// 이전 단계를 건너뛰고 궁수·닌자 시트 임포트 + 프리팹 연결 + 스폰 높이만 다시 적용합니다.
        /// 스프라이트 참조가 끊겼을 때 복구용으로도 사용합니다. log에 진행 내용을 기록합니다.
        /// </summary>
        public static bool ApplyEnemiesOnly(StringBuilder log)
        {
            bool ok = true;
            foreach (var e in Enemies)
            {
                Sprite[] idle = ImportSheet($"{ArtFolder}/{e.file}_idle.png", log);
                Sprite[] attack = ImportSheet($"{ArtFolder}/{e.file}_attack.png", log);
                Sprite[] death = ImportSheet($"{ArtFolder}/{e.file}_death.png", log);
                if (idle.Length == 0 || attack.Length == 0)
                {
                    Debug.LogError($"[SamuraiEnemyAnimApplier] 시트를 찾을 수 없습니다: {ArtFolder}/{e.file}_*.png");
                    log?.AppendLine($"ERROR: sheets missing for {e.file}");
                    ok = false;
                    continue;
                }
                UpdateEnemyPrefab($"Assets/Prefabs/{e.prefab}.prefab", e, idle, attack, death, log);
            }

            UpdateSpawnerHeights();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return ok;
        }

        /// <summary>시트를 픽셀아트 설정으로 임포트하고 모든 프레임의 피벗을 발밑으로 통일합니다. 슬라이스가 없으면 64x48 격자로 자릅니다.</summary>
        internal static Sprite[] ImportSheet(string path, StringBuilder log = null)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                log?.AppendLine($"  no importer: {path}");
                return new Sprite[0];
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

#pragma warning disable 618 // spritesheet API는 구식 표기지만 여전히 동작하며 스프라이트 ID를 유지합니다
            SpriteMetaData[] sheet = importer.spritesheet;
            if (sheet == null || sheet.Length == 0)
            {
                // 슬라이스 정보가 없으면 셀 크기대로 격자 슬라이스
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int count = tex != null ? Mathf.Max(1, tex.width / CellWidth) : 1;
                string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
                sheet = new SpriteMetaData[count];
                for (int i = 0; i < count; i++)
                {
                    sheet[i] = new SpriteMetaData
                    {
                        name = $"{baseName}_{i}",
                        rect = new Rect(i * CellWidth, 0, CellWidth, CellHeight),
                    };
                }
                log?.AppendLine($"  {path}: no slices found -> grid sliced into {count}");
            }
            for (int i = 0; i < sheet.Length; i++)
            {
                sheet[i].alignment = (int)SpriteAlignment.Custom;
                sheet[i].pivot = FeetPivot;
            }
            importer.spritesheet = sheet;
#pragma warning restore 618
            importer.SaveAndReimport();

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s =>
                {
                    int i = s.name.LastIndexOf('_');
                    return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0;
                })
                .ToArray();

            if (log != null)
            {
                var ids = sprites.Select(s =>
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string _, out long id) ? $"{s.name}={id}" : $"{s.name}=?");
                log.AppendLine($"  {path}: {sprites.Length} sprites [{string.Join(", ", ids)}]");
            }
            return sprites;
        }

        internal static void UpdateEnemyPrefab(string path, EnemyArt e, Sprite[] idle, Sprite[] attack, Sprite[] death, StringBuilder log = null)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[SamuraiEnemyAnimApplier] 프리팹이 없습니다: {path}");
                log?.AppendLine($"  prefab missing: {path}");
                return;
            }

            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                go.transform.localScale = Vector3.one;

                var sr = go.GetComponent<SpriteRenderer>();
                log?.AppendLine($"  {path}: sprite before = {(sr != null && sr.sprite != null ? sr.sprite.name : "NULL")}");
                if (sr != null) { sr.sprite = idle[0]; sr.color = Color.white; }

                var anim = go.GetComponent<SpriteSheetAnimator>();
                if (anim == null) anim = go.AddComponent<SpriteSheetAnimator>();
                var clips = new List<SpriteAnimationClip>
                {
                    new SpriteAnimationClip { clipName = "IDLE",   frames = idle,   framesPerSecond = e.idleFps,   loop = true  },
                    new SpriteAnimationClip { clipName = "ATTACK", frames = attack, framesPerSecond = e.attackFps, loop = false },
                };
                if (death.Length > 0)
                    clips.Add(new SpriteAnimationClip { clipName = "DEATH", frames = death, framesPerSecond = e.deathFps, loop = false });
                anim.SetClips(clips.ToArray());

                // 몸통 콜라이더 (모자·활·뻗은 팔은 제외해서 억울한 접촉 피해를 줄임)
                var col = go.GetComponent<BoxCollider2D>();
                if (col != null) { col.size = e.colliderSize; col.offset = e.colliderOffset; }

                var thrower = go.GetComponent<ThrowerEnemy>();
                if (thrower != null)
                {
                    var so = new SerializedObject(thrower);
                    so.FindProperty("animator").objectReferenceValue = anim;
                    so.FindProperty("idleClip").stringValue = "IDLE";
                    so.FindProperty("attackClip").stringValue = "ATTACK";
                    so.FindProperty("deathClip").stringValue = "DEATH";
                    so.FindProperty("releaseFrame").intValue = e.releaseFrame;
                    so.FindProperty("firePointOffset").vector2Value = e.firePoint;
                    so.ApplyModifiedProperties();
                }

                PrefabUtility.SaveAsPrefabAsset(go, path);
                log?.AppendLine($"  {path}: relinked -> idle {idle.Length}, attack {attack.Length}, death {death.Length} frames");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        /// <summary>피벗이 발밑이므로 스폰 높이 = 바닥 윗면.</summary>
        internal static void UpdateSpawnerHeights()
        {
            var spawnerGO = GameObject.Find("Spawner");
            var spawner = spawnerGO != null ? spawnerGO.GetComponent<Spawner>() : null;
            if (spawner == null) return;

            foreach (var e in Enemies)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{e.prefab}.prefab");
                if (prefab == null) continue;
                foreach (var entry in spawner.entries)
                    if (entry.prefab == prefab) entry.spawnY = GroundTopY;
            }
            EditorUtility.SetDirty(spawner);
        }
    }
}
