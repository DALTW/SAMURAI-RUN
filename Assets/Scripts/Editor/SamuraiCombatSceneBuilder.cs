using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SamuraiRunner.Combat;
using SamuraiRunner.Common;
using SamuraiRunner.Effects;
using SamuraiRunner.Enemies;
using SamuraiRunner.Player;
using SamuraiRunner.Spawning;
using SamuraiRunner.Systems;
using SamuraiRunner.UI;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 2단계(전투) 씬 구성 에디터 스크립트.
    /// Tools > Samurai Runner > 2단계 - 전투 요소 추가 를 실행하면:
    ///   1) 1단계(플레이어/바닥/카메라)가 없으면 먼저 구성
    ///   2) Player/Projectile/Enemy/Obstacle 레이어 생성
    ///   3) 프리팹 생성: 화살, 수리검, 궁수, 닌자, 장애물, 칼빛 스파크 (이미 있으면 유지)
    ///   4) 플레이어에 체력 추가 + 칼 판정 레이어 연결
    ///   5) GameSystems(히트스톱·콤보·거리·연출·게임흐름), Spawner, HUD 배치
    /// 실행 후 모든 수치·프리팹 참조는 Scene과 프리팹에서 자유롭게 수정하면 됩니다.
    /// </summary>
    public static class SamuraiCombatSceneBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Tools/Samurai Runner/2단계 - 전투 요소 추가")]
        public static void BuildCombat()
        {
            SamuraiSceneBuilder.BuildScene();

            SamuraiSceneBuilder.EnsureLayerExists("Player");
            SamuraiSceneBuilder.EnsureLayerExists("Projectile");
            SamuraiSceneBuilder.EnsureLayerExists("Enemy");
            SamuraiSceneBuilder.EnsureLayerExists("Obstacle");

            Sprite square = SamuraiSceneBuilder.CreateSquareSprite();
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[SamuraiCombatSceneBuilder] Player를 찾을 수 없습니다.");
                return;
            }

            SetupPlayer(player);

            GameObject spark = BuildSparkPrefab(square);
            GameObject arrow = BuildArrowPrefab(square);
            GameObject shuriken = BuildShurikenPrefab(square);
            GameObject archer = BuildThrowerPrefab(square, "Archer",
                new Color(0.55f, 0.16f, 0.16f), arrow, spark, fireInterval: 1.7f);
            GameObject ninja = BuildThrowerPrefab(square, "Ninja",
                new Color(0.32f, 0.22f, 0.45f), shuriken, spark, fireInterval: 2.1f);
            GameObject obstacle = BuildObstaclePrefab(square);

            GameObject systems = BuildSystems(player, spark);
            BuildSpawner(player, obstacle, archer, ninja);
            BuildHUD(player, systems);

            EnsureSceneInBuildSettings();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SamuraiCombatSceneBuilder] 전투 요소 구성 완료! Z=점프, X=베기(패링), R=재시작. " +
                      "스폰 간격·패링 판정 등은 Scene에서 조절하세요.");
        }

        // ---------- 플레이어 확장 ----------
        private static void SetupPlayer(GameObject player)
        {
            player.layer = LayerMask.NameToLayer("Player");
            GetOrAdd<PlayerHealth>(player);

            var attack = GetOrAdd<PlayerAttack>(player);
            var so = new SerializedObject(attack);
            so.FindProperty("hittableLayers").intValue = 1 << LayerMask.NameToLayer("Projectile");
            so.FindProperty("hitActiveFrom").floatValue = 0.04f;
            so.FindProperty("hitActiveUntil").floatValue = 0.3f;
            so.FindProperty("justParryWindow").floatValue = 0.15f;
            so.ApplyModifiedProperties();
        }

        // ---------- 프리팹 ----------
        private static GameObject BuildPrefab(string name, System.Action<GameObject> configure)
        {
            Directory.CreateDirectory(PrefabFolder);
            string path = $"{PrefabFolder}/{name}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing; // 이미 있으면 사용자 수정을 보호하기 위해 유지

            var go = new GameObject(name);
            try
            {
                configure(go);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static GameObject BuildSparkPrefab(Sprite square)
        {
            return BuildPrefab("Effect_Spark", go =>
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.sortingOrder = 10;
                go.transform.localScale = new Vector3(1.1f, 0.12f, 1f); // 가늘고 긴 칼빛 궤적
                go.AddComponent<FlashEffect>();
            });
        }

        private static GameObject BuildArrowPrefab(Sprite square)
        {
            return BuildPrefab("Projectile_Arrow", go =>
            {
                go.layer = LayerMask.NameToLayer("Projectile");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = new Color(0.93f, 0.83f, 0.5f);
                go.transform.localScale = new Vector3(0.8f, 0.1f, 1f);

                AddKinematicTrigger(go);
                go.AddComponent<Projectile>(); // 기본값: 직선 비행, 방향 바라봄
            });
        }

        private static GameObject BuildShurikenPrefab(Sprite square)
        {
            return BuildPrefab("Projectile_Shuriken", go =>
            {
                go.layer = LayerMask.NameToLayer("Projectile");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = new Color(0.75f, 0.78f, 0.82f);
                go.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

                AddKinematicTrigger(go);
                var projectile = go.AddComponent<Projectile>();
                var so = new SerializedObject(projectile);
                so.FindProperty("speed").floatValue = 8.5f;
                so.FindProperty("spinSpeed").floatValue = 540f;
                so.FindProperty("rotateTowardsDirection").boolValue = false;
                so.ApplyModifiedProperties();
            });
        }

        private static GameObject BuildThrowerPrefab(Sprite square, string name,
            Color color, GameObject projectilePrefab, GameObject sparkPrefab, float fireInterval)
        {
            return BuildPrefab(name, go =>
            {
                go.layer = LayerMask.NameToLayer("Enemy");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = color;
                go.transform.localScale = new Vector3(0.6f, 1.4f, 1f);

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;

                go.AddComponent<DamageOnTouch>();
                go.AddComponent<DespawnBehind>();

                var thrower = go.AddComponent<ThrowerEnemy>();
                thrower.projectilePrefab = projectilePrefab.GetComponent<Projectile>();
                thrower.deathEffectPrefab = sparkPrefab.GetComponent<FlashEffect>();
                var so = new SerializedObject(thrower);
                so.FindProperty("fireInterval").floatValue = fireInterval;
                so.ApplyModifiedProperties();
            });
        }

        private static GameObject BuildObstaclePrefab(Sprite square)
        {
            return BuildPrefab("Obstacle_Rock", go =>
            {
                go.layer = LayerMask.NameToLayer("Obstacle");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = new Color(0.45f, 0.42f, 0.38f);
                go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;

                go.AddComponent<DamageOnTouch>();
                go.AddComponent<DespawnBehind>();
            });
        }

        private static void AddKinematicTrigger(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        // ---------- 시스템 ----------
        private static GameObject BuildSystems(GameObject player, GameObject sparkPrefab)
        {
            GameObject systems = GameObject.Find("GameSystems") ?? new GameObject("GameSystems");

            GetOrAdd<HitStop>(systems);

            var combo = GetOrAdd<ComboCounter>(systems);
            combo.playerAttack = player.GetComponent<PlayerAttack>();
            combo.playerHealth = player.GetComponent<PlayerHealth>();

            var distance = GetOrAdd<DistanceTracker>(systems);
            distance.player = player.transform;

            var effects = GetOrAdd<ParryEffects>(systems);
            effects.playerAttack = player.GetComponent<PlayerAttack>();
            effects.sparkPrefab = sparkPrefab.GetComponent<FlashEffect>();

            var flow = GetOrAdd<GameFlowController>(systems);
            flow.playerHealth = player.GetComponent<PlayerHealth>();
            flow.playerMotor = player.GetComponent<PlayerMotor>();
            flow.playerAttack = player.GetComponent<PlayerAttack>();

            return systems;
        }

        private static void BuildSpawner(GameObject player,
            GameObject obstacle, GameObject archer, GameObject ninja)
        {
            GameObject spawnerGO = GameObject.Find("Spawner") ?? new GameObject("Spawner");
            var spawner = GetOrAdd<Spawner>(spawnerGO);
            spawner.player = player.transform;

            // 이미 항목이 있으면 사용자 설정을 보호하기 위해 그대로 둡니다.
            if (spawner.entries.Count == 0)
            {
                // 기획의 3단계 난이도: 장애물만 → 궁수 추가 → 닌자(빠른 수리검) 추가
                spawner.entries.Add(new SpawnEntry
                {
                    name = "장애물 (바위)", prefab = obstacle,
                    minInterval = 2.5f, maxInterval = 4.5f, spawnY = -1.05f, startDelay = 2f
                });
                spawner.entries.Add(new SpawnEntry
                {
                    name = "궁수", prefab = archer,
                    minInterval = 5f, maxInterval = 8f, spawnY = -0.8f, startDelay = 12f
                });
                spawner.entries.Add(new SpawnEntry
                {
                    name = "수리검 닌자", prefab = ninja,
                    minInterval = 6f, maxInterval = 9f, spawnY = -0.8f, startDelay = 30f
                });
            }
        }

        // ---------- HUD ----------
        private static void BuildHUD(GameObject player, GameObject systems)
        {
            GameObject hudGO = GameObject.Find("HUD") ?? new GameObject("HUD");
            var canvas = GetOrAdd<Canvas>(hudGO);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetOrAdd<CanvasScaler>(hudGO);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            GetOrAdd<GraphicRaycaster>(hudGO);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite square = SamuraiSceneBuilder.CreateSquareSprite();

            // 심장 3개 (좌상단)
            var heartsRoot = FindOrCreateUIChild(hudGO.transform, "Hearts",
                new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(150f, 40f));
            var hearts = new Image[3];
            for (int i = 0; i < hearts.Length; i++)
            {
                var heart = FindOrCreateUIChild(heartsRoot.transform, $"Heart{i}",
                    new Vector2(0f, 1f), new Vector2(i * 38f, 0f), new Vector2(28f, 28f));
                hearts[i] = GetOrAdd<Image>(heart);
                hearts[i].sprite = square;
            }

            // 콤보 (상단 중앙)
            var comboGO = FindOrCreateUIChild(hudGO.transform, "ComboText",
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(400f, 50f));
            var comboText = GetOrAdd<Text>(comboGO);
            comboText.font = font;
            comboText.fontSize = 34;
            comboText.fontStyle = FontStyle.Bold;
            comboText.alignment = TextAnchor.MiddleCenter;
            comboText.color = new Color(1f, 0.85f, 0.4f);
            comboText.text = string.Empty;

            // 거리 (우상단)
            var distGO = FindOrCreateUIChild(hudGO.transform, "DistanceText",
                new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(220f, 36f));
            var distText = GetOrAdd<Text>(distGO);
            distText.font = font;
            distText.fontSize = 24;
            distText.alignment = TextAnchor.MiddleRight;
            distText.color = new Color(0.92f, 0.9f, 0.85f);
            distText.text = "0 m";

            // 게임오버 (중앙, 기본 비활성)
            var gameOverGO = FindOrCreateUIChild(hudGO.transform, "GameOver",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 200f));
            var gameOverText = GetOrAdd<Text>(gameOverGO);
            gameOverText.font = font;
            gameOverText.fontSize = 32;
            gameOverText.alignment = TextAnchor.MiddleCenter;
            gameOverText.color = new Color(0.92f, 0.88f, 0.8f);
            gameOverText.text = "노검객의 질주가 끝났다\n\nR - 다시 달린다";
            gameOverGO.SetActive(false);

            // HUD 컴포넌트 연결
            var hud = GetOrAdd<GameHUD>(hudGO);
            hud.playerHealth = player.GetComponent<PlayerHealth>();
            hud.comboCounter = systems.GetComponent<ComboCounter>();
            hud.distanceTracker = systems.GetComponent<DistanceTracker>();
            hud.heartIcons = hearts;
            hud.comboText = comboText;
            hud.distanceText = distText;

            // 게임오버 UI를 게임 흐름에 연결
            var flow = systems.GetComponent<GameFlowController>();
            if (flow != null) flow.gameOverUI = gameOverGO;
        }

        private static GameObject FindOrCreateUIChild(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return go;
        }

        // ---------- 기타 ----------
        private static void EnsureSceneInBuildSettings()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) return;

            if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(scene.path, true))
                    .ToArray();
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out T c) ? c : go.AddComponent<T>();
    }
}
