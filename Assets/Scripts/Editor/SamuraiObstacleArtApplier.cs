using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.Combat;
using SamuraiRunner.Common;
using SamuraiRunner.Spawning;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 3단계: 픽셀 장애물 스프라이트를 프리팹과 스포너에 적용하는 에디터 스크립트.
    /// Tools > Samurai Runner > 3단계 - 픽셀 장애물 적용 을 실행하면:
    ///   1) 2단계(전투 요소)가 없으면 먼저 구성
    ///   2) Assets/Art/Obstacles의 도트 3종(바위·통나무·죽창)을 픽셀아트 설정으로 임포트
    ///   3) 장애물 프리팹을 만들거나 갱신 (스프라이트 교체 + 콜라이더를 그림에 맞게 자동 조정)
    ///   4) Spawner 항목을 연결 (있으면 프리팹·높이만 갱신, 간격 등 사용자 수치는 유지)
    /// 이후 콜라이더·스폰 수치는 프리팹/Scene에서 자유롭게 수정하면 됩니다.
    /// </summary>
    public static class SamuraiObstacleArtApplier
    {
        private const string ArtFolder = "Assets/Art/Obstacles";
        private const string PrefabFolder = "Assets/Prefabs";
        private const int PixelsPerUnit = 32;

        [Tooltip("바닥 윗면의 월드 Y — Ground 위치를 바꿨다면 여기도 맞춰주세요")]
        private const float GroundTopY = -1.5f;

        // (스프라이트 파일, 프리팹 이름, 스포너 표시 이름, 스폰 간격 최소/최대, 스폰 시작 시각)
        private static readonly (string file, string prefabName, string entryName,
            float minInterval, float maxInterval, float startDelay)[] Obstacles =
        {
            ("obstacle_rock.png",   "Obstacle_Rock",   "장애물 (바위)",   2.5f, 4.5f, 2f),
            ("obstacle_log.png",    "Obstacle_Log",    "장애물 (통나무)", 4f,   7f,   6f),
            ("obstacle_spikes.png", "Obstacle_Spikes", "장애물 (죽창)",   5f,   8f,   10f),
        };

        [MenuItem("Tools/Samurai Runner/3단계 - 픽셀 장애물 적용")]
        public static void Apply()
        {
            SamuraiCombatSceneBuilder.BuildCombat();

            var spawnerGO = GameObject.Find("Spawner");
            var spawner = spawnerGO != null ? spawnerGO.GetComponent<Spawner>() : null;

            foreach (var o in Obstacles)
            {
                Sprite sprite = ImportPixelSprite($"{ArtFolder}/{o.file}");
                if (sprite == null)
                {
                    Debug.LogError($"[SamuraiObstacleArtApplier] 스프라이트가 없습니다: {ArtFolder}/{o.file}");
                    continue;
                }

                GameObject prefab = CreateOrUpdatePrefab(o.prefabName, sprite);
                if (spawner != null)
                    LinkSpawnerEntry(spawner, prefab, sprite, o.entryName, o.minInterval, o.maxInterval, o.startDelay);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SamuraiObstacleArtApplier] 픽셀 장애물 적용 완료! (바위·통나무·죽창)");
        }

        private static Sprite ImportPixelSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return null;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject CreateOrUpdatePrefab(string name, Sprite sprite)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing == null)
            {
                var go = new GameObject(name);
                try
                {
                    go.layer = LayerMask.NameToLayer("Obstacle");
                    go.AddComponent<SpriteRenderer>();
                    var col = go.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    go.AddComponent<DamageOnTouch>();
                    go.AddComponent<DespawnBehind>();
                    ConfigureVisual(go, sprite);
                    return PrefabUtility.SaveAsPrefabAsset(go, path);
                }
                finally { Object.DestroyImmediate(go); }
            }

            // 기존 프리팹: 스프라이트와 콜라이더만 갱신, 나머지 사용자 수정은 유지
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureVisual(contents, sprite);
                return PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void ConfigureVisual(GameObject go, Sprite sprite)
        {
            go.transform.localScale = Vector3.one; // 단색 사각형 시절의 스케일 제거

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = sprite; sr.color = Color.white; }

            var col = go.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                // 그림보다 약간 작게 → 아슬아슬한 점프가 억울하지 않게
                Vector2 size = sprite.bounds.size;
                col.size = new Vector2(size.x * 0.8f, size.y * 0.85f);
                col.offset = new Vector2(0f, -size.y * 0.06f);
            }
        }

        private static void LinkSpawnerEntry(Spawner spawner, GameObject prefab, Sprite sprite,
            string entryName, float minInterval, float maxInterval, float startDelay)
        {
            // 바닥 위에 정확히 앉도록 스폰 높이 계산 (피벗 = 스프라이트 중앙)
            float spawnY = GroundTopY + sprite.bounds.extents.y;

            var entry = spawner.entries.FirstOrDefault(e => e.prefab == prefab)
                     ?? spawner.entries.FirstOrDefault(e => e.name == entryName);

            if (entry != null)
            {
                entry.prefab = prefab;
                entry.name = entryName;
                entry.spawnY = spawnY; // 새 그림 크기에 맞춰 높이만 갱신
            }
            else
            {
                spawner.entries.Add(new SpawnEntry
                {
                    name = entryName,
                    prefab = prefab,
                    minInterval = minInterval,
                    maxInterval = maxInterval,
                    spawnY = spawnY,
                    startDelay = startDelay,
                });
            }
            EditorUtility.SetDirty(spawner);
        }
    }
}
