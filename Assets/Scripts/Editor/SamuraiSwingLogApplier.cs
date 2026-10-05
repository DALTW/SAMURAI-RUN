using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.Combat;
using SamuraiRunner.Common;
using SamuraiRunner.Player;
using SamuraiRunner.Spawning;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 8단계: 위에서 휘둘러 내려오는 통나무 함정 추가 에디터 스크립트.
    /// Tools > Samurai Runner > 8단계 - 흔들리는 통나무 함정 추가 를 실행하면:
    ///   1) 이전 단계(1~7)가 없으면 먼저 구성
    ///   2) Trap_SwingLog 프리팹(통나무 그림 + 트리거 + SwingingLogTrap, Projectile 레이어 → 칼로 튕겨낼 수 있음)을 만들거나 갱신
    ///   3) Spawner에 "흔들리는 통나무" 항목이 없으면 추가 (매다는 높이 3.8 = 화면 위 밖)
    /// 처음 한 번은 스크립트 로드 시 자동으로 "검증"만 하고, 빠진 것이 있을 때만 2)~3)을 실행합니다
    /// (Library/SamuraiSwingLogSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiSwingLogSetup.report.txt).
    /// 흔들림 속도·내려오는 높이·튕기는 세기는 프리팹에서, 등장 간격은 Spawner에서 조절합니다.
    /// </summary>
    public static class SamuraiSwingLogApplier
    {
        private const string PrefabPath = "Assets/Prefabs/Trap_SwingLog.prefab";
        private const string LogSpritePath = "Assets/Art/Obstacles/obstacle_log.png";
        private const string EntryName = "흔들리는 통나무";
        private const float DefaultPivotY = 3.8f;
        private const int SortingOrder = 13; // 플레이어(10)·투사체(12) 앞
        private const string MarkerPath = "Library/SamuraiSwingLogSetup.done";
        private const string ReportPath = "Library/SamuraiSwingLogSetup.report.txt";
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

        [MenuItem("Tools/Samurai Runner/8단계 - 흔들리는 통나무 함정 추가")]
        public static void Apply()
        {
            SamuraiPitApplier.Apply();
            Run(auto: false, forceRepair: true);
        }

        // ---------- 최초 1회 자동 검증 ----------
        [InitializeOnLoadMethod]
        private static void AutoVerifyOnce()
        {
            if (File.Exists(MarkerPath)) return;
            idleSince = -1;
            EditorApplication.update += WaitForIdleThenVerify;
        }

        private static void WaitForIdleThenVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                idleSince = -1;
                return;
            }
            if (idleSince < 0) { idleSince = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - idleSince < SettleSeconds) return;

            EditorApplication.update -= WaitForIdleThenVerify;
            if (File.Exists(MarkerPath)) return;
            Run(auto: true, forceRepair: false);
        }

        private static void Run(bool auto, bool forceRepair)
        {
            var log = new StringBuilder();
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiSwingLogApplier auto={auto} force={forceRepair}");
            var scene = EditorSceneManager.GetActiveScene();
            bool sceneWasClean = !scene.isDirty;
            log.AppendLine($"scene: {scene.path} dirty(before)={scene.isDirty}");
            try
            {
                log.AppendLine("verify(before):");
                bool ok = Verify(log);
                if (!ok || forceRepair)
                {
                    log.AppendLine(ok ? "repair: forced" : "repair: needed");
                    bool sceneChanged = Repair(log);
                    AssetDatabase.SaveAssets();
                    if (sceneChanged)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (auto && sceneWasClean && !string.IsNullOrEmpty(scene.path))
                        {
                            EditorSceneManager.SaveScene(scene);
                            log.AppendLine("scene saved");
                        }
                        else log.AppendLine("scene left dirty — save it manually (Ctrl+S)");
                    }
                    log.AppendLine("verify(after):");
                    ok = Verify(log);
                }
                log.AppendLine(ok ? "result: OK" : "result: FAILED (see above)");
                if (ok) Debug.Log($"[SamuraiSwingLogApplier] 흔들리는 통나무 함정 설정 확인 완료. 리포트: {ReportPath}");
                else Debug.LogWarning($"[SamuraiSwingLogApplier] 흔들리는 통나무 함정 설정을 확인하지 못했습니다. 리포트: {ReportPath}");
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

        // ---------- 검증 ----------
        private static bool Verify(StringBuilder log)
        {
            bool ok = true;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (prefab == null) { log.AppendLine($"  {PrefabPath}: MISSING"); ok = false; }
            else
            {
                var sr = prefab.GetComponent<SpriteRenderer>();
                var col = prefab.GetComponent<BoxCollider2D>();
                var rb = prefab.GetComponent<Rigidbody2D>();
                var trap = prefab.GetComponent<SwingingLogTrap>();
                var despawn = prefab.GetComponent<DespawnBehind>();
                log.AppendLine($"  {PrefabPath}: layer={LayerMask.LayerToName(prefab.layer)}, sprite={(sr != null && sr.sprite != null ? sr.sprite.name : "NULL")}, " +
                               $"order={(sr != null ? sr.sortingOrder : -1)}, trigger={(col != null && col.isTrigger)}, rigidbody={(rb != null ? rb.bodyType.ToString() : "none")}, " +
                               $"SwingingLogTrap={(trap != null)}, DespawnBehind={(despawn != null)}");
                if (prefab.layer != projectileLayer || sr == null || sr.sprite == null || col == null || !col.isTrigger || rb == null || trap == null)
                    ok = false;

                // 칼(PlayerAttack)이 이 레이어를 벨 수 있는지
                var attack = Object.FindAnyObjectByType<PlayerAttack>();
                if (attack != null)
                {
                    int mask = new SerializedObject(attack).FindProperty("hittableLayers").intValue;
                    bool canHit = (mask & (1 << prefab.layer)) != 0;
                    log.AppendLine($"  PlayerAttack.hittableLayers={mask} → sword can parry trap: {canHit}");
                    if (!canHit) ok = false;
                }

                if (trap != null) LogTiming(log, trap);
            }

            var spawner = Object.FindAnyObjectByType<Spawner>();
            if (spawner == null) { log.AppendLine("  Spawner: not in scene"); ok = false; }
            else
            {
                var entry = prefab != null ? spawner.entries.FirstOrDefault(e => e.prefab == prefab) : null;
                if (entry == null) { log.AppendLine("  Spawner: no entry for Trap_SwingLog"); ok = false; }
                else log.AppendLine($"  Spawner entry '{entry.name}': enabled={entry.enabled}, interval={entry.minInterval}~{entry.maxInterval}s, " +
                                    $"startDelay={entry.startDelay}s, spawnY(pivot)={entry.spawnY}, allowNearPits={entry.allowNearPits}");
            }
            return ok;
        }

        /// <summary>프리팹 값으로 흔들림 시간·놓는 거리를 계산해 리포트에 남깁니다 (게임 코드와 같은 적분).</summary>
        private static void LogTiming(StringBuilder log, SwingingLogTrap trap)
        {
            var so = new SerializedObject(trap);
            float bottomY = so.FindProperty("bottomY").floatValue;
            float startAngle = so.FindProperty("startAngle").floatValue;
            float g = so.FindProperty("swingGravity").floatValue;
            float attachY = so.FindProperty("ropeAttachY").floatValue;

            var spawner = Object.FindAnyObjectByType<Spawner>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var entry = spawner != null ? spawner.entries.FirstOrDefault(e => e.prefab == prefab) : null;
            float pivotY = entry != null ? entry.spawnY : DefaultPivotY;
            float rope = Mathf.Max(0.5f, pivotY - bottomY - attachY);

            float th = startAngle * Mathf.Deg2Rad, om = 0f, t = 0f, h = 1f / 240f, k = g / rope;
            while (th > 0f && t < 5f) { om += -k * Mathf.Sin(th) * h; th += om * h; t += h; }

            float run = 5f;
            var motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null) run = new SerializedObject(motor).FindProperty("runSpeed").floatValue;
            float bottomSpeed = Mathf.Sqrt(2f * g * rope * (1f - Mathf.Cos(startAngle * Mathf.Deg2Rad)));
            log.AppendLine($"  timing: rope={rope:0.00}, bottomY={bottomY}, time to bottom={t:0.00}s, release when player is {run * t:0.0} units before pivot, bottom speed≈{bottomSpeed:0.0}");
        }

        // ---------- 복구/생성 ----------
        /// <returns>씬(Spawner)을 바꿨으면 true</returns>
        private static bool Repair(StringBuilder log)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogSpritePath);
            if (sprite == null) { log.AppendLine($"  ERROR: {LogSpritePath} 스프라이트가 없습니다"); return false; }

            GameObject prefab = CreateOrUpdatePrefab(sprite, log);
            if (prefab == null) return false;

            var spawner = Object.FindAnyObjectByType<Spawner>();
            if (spawner == null) { log.AppendLine("  Spawner: not in scene — entry not added"); return false; }
            if (spawner.entries.Any(e => e.prefab == prefab)) return false;

            Undo.RecordObject(spawner, "Add swinging log entry");
            spawner.entries.Add(new SpawnEntry
            {
                name = EntryName,
                prefab = prefab,
                minInterval = 7f,
                maxInterval = 11f,
                spawnY = DefaultPivotY,
                startDelay = 20f,
            });
            EditorUtility.SetDirty(spawner);
            log.AppendLine("  Spawner entry added");
            return true;
        }

        private static GameObject CreateOrUpdatePrefab(Sprite sprite, StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var go = new GameObject("Trap_SwingLog");
                try
                {
                    Configure(go, sprite);
                    log.AppendLine($"  {PrefabPath}: created");
                    return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                }
                finally { Object.DestroyImmediate(go); }
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Configure(contents, sprite);
                log.AppendLine($"  {PrefabPath}: updated");
                return PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// <summary>콜라이더를 SwingingLogTrap보다 먼저 붙입니다 (RequireComponent가 추상 Collider2D라서).</summary>
        private static void Configure(GameObject go, Sprite sprite)
        {
            go.layer = LayerMask.NameToLayer("Projectile"); // 칼 판정(hittableLayers)에 걸리게
            go.transform.localScale = Vector3.one;

            var sr = GetOrAdd<SpriteRenderer>(go);
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = SortingOrder;

            var rb = GetOrAdd<Rigidbody2D>(go);
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var col = GetOrAdd<BoxCollider2D>(go);
            col.isTrigger = true;
            Vector2 size = sprite.bounds.size;
            col.size = new Vector2(size.x * 0.85f, size.y * 0.82f); // 위로 솟은 가지는 빼고 몸통만
            col.offset = new Vector2(0f, -0.03f);

            GetOrAdd<SwingingLogTrap>(go);
            GetOrAdd<DespawnBehind>(go);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out T c) ? c : go.AddComponent<T>();
    }
}
