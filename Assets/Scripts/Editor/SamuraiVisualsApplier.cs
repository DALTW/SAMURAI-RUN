using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.Environment;
using SamuraiRunner.Spawning;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 4단계: 적·무기·배경 스프라이트를 게임에 적용하는 에디터 스크립트.
    /// Tools > Samurai Runner > 4단계 - 적·무기·배경 적용 을 실행하면:
    ///   1) 이전 단계(1~3)가 없으면 먼저 구성
    ///   2) Assets/Art의 적·무기·배경 도트를 픽셀아트 설정으로 임포트
    ///   3) 궁수·닌자·화살·수리검 프리팹의 스프라이트/콜라이더 갱신 (다른 수치는 유지)
    ///   4) 하늘·원경·근경 패럴랙스 배경 배치, 바닥을 흙+풀 타일로 교체
    ///   5) 그리기 순서(sorting order) 정리, 스포너 높이 갱신
    /// 이후 모든 수치는 Scene/프리팹에서 자유롭게 수정하면 됩니다.
    /// </summary>
    public static class SamuraiVisualsApplier
    {
        private const int PixelsPerUnit = 32;
        private const float GroundTopY = -1.5f;

        // 그리기 순서: 하늘 -30 < 원경 -25 < 근경 -20 < 바닥 0 < 장애물 4 < 적 5 < 플레이어 10 < 투사체 12 < 이펙트 15
        private const int OrderObstacle = 4, OrderEnemy = 5, OrderPlayer = 10, OrderProjectile = 12, OrderEffect = 15;

        [MenuItem("Tools/Samurai Runner/4단계 - 적·무기·배경 적용")]
        public static void Apply()
        {
            SamuraiObstacleArtApplier.Apply();

            // ---------- 임포트 ----------
            Sprite archer = ImportSprite("Assets/Art/Enemies/enemy_archer.png", fullRect: false);
            Sprite ninja = ImportSprite("Assets/Art/Enemies/enemy_ninja.png", fullRect: false);
            Sprite arrow = ImportSprite("Assets/Art/Weapons/weapon_arrow.png", fullRect: false);
            Sprite shuriken = ImportSprite("Assets/Art/Weapons/weapon_shuriken.png", fullRect: false);
            Sprite sky = ImportSprite("Assets/Art/Backgrounds/bg_sky.png", fullRect: true);
            Sprite far = ImportSprite("Assets/Art/Backgrounds/bg_bamboo_far.png", fullRect: true);
            Sprite near = ImportSprite("Assets/Art/Backgrounds/bg_bamboo_near.png", fullRect: true);
            Sprite ground = ImportSprite("Assets/Art/Backgrounds/ground_tile.png", fullRect: true);

            if (archer == null || sky == null)
            {
                Debug.LogError("[SamuraiVisualsApplier] Assets/Art 아래의 스프라이트를 찾을 수 없습니다.");
                return;
            }

            // ---------- 프리팹 ----------
            UpdateCharacterPrefab("Assets/Prefabs/Archer.prefab", archer, OrderEnemy, new Vector2(-0.5f, 0.05f));
            UpdateCharacterPrefab("Assets/Prefabs/Ninja.prefab", ninja, OrderEnemy, new Vector2(-0.4f, 0.12f));
            UpdateProjectilePrefab("Assets/Prefabs/Projectile_Arrow.prefab", arrow);
            UpdateProjectilePrefab("Assets/Prefabs/Projectile_Shuriken.prefab", shuriken);
            foreach (string name in new[] { "Obstacle_Rock", "Obstacle_Log", "Obstacle_Spikes" })
                SetPrefabSortingOrder($"Assets/Prefabs/{name}.prefab", OrderObstacle);
            SetPrefabSortingOrder("Assets/Prefabs/Effect_Spark.prefab", OrderEffect);

            // ---------- 씬 ----------
            var player = GameObject.Find("Player");
            if (player != null && player.TryGetComponent<SpriteRenderer>(out var playerSR))
                playerSR.sortingOrder = OrderPlayer;

            BuildGroundTile(ground);
            BuildBackground(sky, far, near);
            UpdateSpawnerHeights(archer, ninja);

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(96f / 255f, 104f / 255f, 132f / 255f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SamuraiVisualsApplier] 적·무기·배경 적용 완료!");
        }

        private static Sprite ImportSprite(string path, bool fullRect)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (fullRect) // 타일(drawMode Tiled) 렌더링용
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void UpdateCharacterPrefab(string path, Sprite sprite, int order, Vector2 firePointOffset)
        {
            EditPrefab(path, go =>
            {
                go.transform.localScale = Vector3.one;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) { sr.sprite = sprite; sr.color = Color.white; sr.sortingOrder = order; }

                var col = go.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Vector2 size = sprite.bounds.size;
                    col.size = new Vector2(size.x * 0.6f, size.y * 0.95f);
                    col.offset = Vector2.zero;
                }

                var thrower = go.GetComponent<Enemies.ThrowerEnemy>();
                if (thrower != null)
                {
                    var so = new SerializedObject(thrower);
                    so.FindProperty("firePointOffset").vector2Value = firePointOffset;
                    so.ApplyModifiedProperties();
                }
            });
        }

        private static void UpdateProjectilePrefab(string path, Sprite sprite)
        {
            EditPrefab(path, go =>
            {
                go.transform.localScale = Vector3.one;
                go.transform.rotation = Quaternion.identity;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) { sr.sprite = sprite; sr.color = Color.white; sr.sortingOrder = OrderProjectile; }

                var col = go.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Vector2 size = sprite.bounds.size;
                    col.size = new Vector2(size.x * 0.9f, size.y * 0.9f);
                    col.offset = Vector2.zero;
                }
            });
        }

        private static void SetPrefabSortingOrder(string path, int order)
        {
            EditPrefab(path, go =>
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = order;
            });
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void BuildGroundTile(Sprite ground)
        {
            var go = GameObject.Find("Ground");
            if (go == null || ground == null) return;

            go.transform.localScale = Vector3.one;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = ground;
                sr.color = Color.white;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(500f, 1f);
                sr.sortingOrder = 0;
            }
            var col = go.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = new Vector2(500f, 1f);
                col.offset = Vector2.zero;
            }
        }

        private static void BuildBackground(Sprite sky, Sprite far, Sprite near)
        {
            var root = GameObject.Find("Background") ?? new GameObject("Background");

            // 하늘: 카메라에 고정 (화면보다 넓은 한 장)
            MakeLayer(root.transform, "BG_Sky", sky, 0f, 0.8f, -30, tiled: false);
            // 원경 안개 대나무: 바닥 위에 앉힘
            MakeLayer(root.transform, "BG_BambooFar", far, 0.15f,
                GroundTopY + far.bounds.extents.y, -25, tiled: true);
            // 근경 대나무
            MakeLayer(root.transform, "BG_BambooNear", near, 0.4f,
                GroundTopY + near.bounds.extents.y, -20, tiled: true);
        }

        private static void MakeLayer(Transform parent, string name, Sprite sprite,
            float factor, float y, int order, bool tiled)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, y, 0f);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (tiled)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(sprite.bounds.size.x * 4f, sprite.bounds.size.y);
            }
            else
            {
                sr.drawMode = SpriteDrawMode.Simple;
            }

            var layer = go.GetComponent<ParallaxLayer>();
            if (layer == null) layer = go.AddComponent<ParallaxLayer>();
            layer.parallaxFactor = factor;
            layer.lockedY = y;
        }

        private static void UpdateSpawnerHeights(Sprite archer, Sprite ninja)
        {
            var spawnerGO = GameObject.Find("Spawner");
            var spawner = spawnerGO != null ? spawnerGO.GetComponent<Spawner>() : null;
            if (spawner == null) return;

            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Archer.prefab");
            var ninjaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ninja.prefab");

            foreach (var entry in spawner.entries)
            {
                if (entry.prefab == archerPrefab) entry.spawnY = GroundTopY + archer.bounds.extents.y;
                if (entry.prefab == ninjaPrefab) entry.spawnY = GroundTopY + ninja.bounds.extents.y;
            }
            EditorUtility.SetDirty(spawner);
        }
    }
}
