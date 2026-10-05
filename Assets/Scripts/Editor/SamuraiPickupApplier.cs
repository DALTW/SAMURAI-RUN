using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using SamuraiRunner.Common;
using SamuraiRunner.Enemies;
using SamuraiRunner.Items;
using SamuraiRunner.Player;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 6단계: 궁수·닌자를 처치하면 체력 회복 주먹밥을 떨어뜨리게 하는 에디터 스크립트.
    /// Tools > Samurai Runner > 6단계 - 처치 시 주먹밥 드롭 을 실행하면:
    ///   1) 이전 단계(1~5)가 없으면 먼저 구성
    ///   2) Pickup_Onigiri 프리팹(주먹밥 스프라이트 + 트리거 + HealthPickup)을 만들거나 갱신
    ///   3) 궁수·닌자 프리팹에 LootDropper를 붙이고 주먹밥 프리팹을 연결
    /// 처음 한 번은 스크립트 로드 시 자동으로 "검증"만 하고, 빠진 것이 있을 때만 2)~3)을 실행합니다
    /// (Library/SamuraiPickupSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiPickupSetup.report.txt).
    /// 드롭 확률·튀어오르는 높이 등은 프리팹에서 자유롭게 수정하면 됩니다.
    /// </summary>
    public static class SamuraiPickupApplier
    {
        private const string PickupPath = "Assets/Prefabs/Pickup_Onigiri.prefab";
        private const string HeartSheetPath = "Assets/Art/UI/hp_onigiri.png";
        private const string MarkerPath = "Library/SamuraiPickupSetup.done";
        private const string ReportPath = "Library/SamuraiPickupSetup.report.txt";
        private const int PickupSortingOrder = 6; // 적(5) 위, 플레이어(10) 아래
        private static readonly string[] EnemyPrefabs = { "Assets/Prefabs/Archer.prefab", "Assets/Prefabs/Ninja.prefab" };

        [MenuItem("Tools/Samurai Runner/6단계 - 처치 시 주먹밥 드롭")]
        public static void Apply()
        {
            SamuraiEnemyAnimApplier.Apply();
            Run(auto: false, forceRepair: true);
        }

        // ---------- 최초 1회 자동 검증 ----------
        // 임포트 직후 같은 순간에 읽으면 다른 프리팹을 가리키는 참조가 아직 비어 보일 수 있어서,
        // 에디터가 한가해진 뒤 잠시(SettleSeconds) 더 기다렸다가 검증합니다.
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

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
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiPickupApplier auto={auto} force={forceRepair}");
            try
            {
                log.AppendLine("verify(before):");
                bool ok = Verify(log);
                if (!ok || forceRepair)
                {
                    log.AppendLine(ok ? "repair: forced" : "repair: needed");
                    Repair(log);
                    AssetDatabase.SaveAssets();
                    log.AppendLine("verify(after):");
                    ok = Verify(log);
                }
                log.AppendLine(ok ? "result: OK" : "result: FAILED (see above)");
                if (ok) Debug.Log($"[SamuraiPickupApplier] 주먹밥 드롭 설정 확인 완료. 리포트: {ReportPath}");
                else Debug.LogError($"[SamuraiPickupApplier] 주먹밥 드롭 설정에 문제가 있습니다. 리포트: {ReportPath}");
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
            var pickupGO = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
            HealthPickup pickup = pickupGO != null ? pickupGO.GetComponent<HealthPickup>() : null;
            if (pickupGO == null) { log.AppendLine($"  {PickupPath}: MISSING"); ok = false; }
            else
            {
                var sr = pickupGO.GetComponent<SpriteRenderer>();
                var col = pickupGO.GetComponent<Collider2D>();
                var rb = pickupGO.GetComponent<Rigidbody2D>();
                int groundBits = 0;
                if (pickup != null) groundBits = new SerializedObject(pickup).FindProperty("groundLayer").intValue;
                int groundMask = 1 << LayerMask.NameToLayer("Ground");
                log.AppendLine($"  {PickupPath}: sprite={(sr != null && sr.sprite != null ? sr.sprite.name : "NULL")}, " +
                               $"trigger={(col != null && col.isTrigger)}, rigidbody={(rb != null ? rb.bodyType.ToString() : "none")}, " +
                               $"HealthPickup={(pickup != null)}, groundLayer={groundBits} (Ground={groundMask})");
                if (sr == null || sr.sprite == null || col == null || !col.isTrigger || rb == null || pickup == null || (groundBits & groundMask) == 0)
                    ok = false;
            }

            foreach (string path in EnemyPrefabs)
            {
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (enemy == null) { log.AppendLine($"  {path}: MISSING"); ok = false; continue; }
                var dropper = enemy.GetComponent<LootDropper>();
                // 저장된 직렬화 데이터 기준으로 판단 (C# 필드는 임포트 직후 잠깐 비어 보일 수 있음)
                Object serializedRef = dropper != null
                    ? new SerializedObject(dropper).FindProperty("dropPrefab").objectReferenceValue
                    : null;
                bool managedOk = dropper != null && dropper.dropPrefab != null && dropper.dropPrefab == pickup;
                bool serializedOk = serializedRef != null && serializedRef == pickup;
                bool linked = pickup != null && (managedOk || serializedOk);
                log.AppendLine($"  {path}: LootDropper={(dropper != null)}, " +
                               $"field={(dropper != null && dropper.dropPrefab != null ? dropper.dropPrefab.name : "NULL")}, " +
                               $"serialized={(serializedRef != null ? serializedRef.name : "NULL")}, linked={linked}");
                if (!linked) ok = false;
            }
            return ok;
        }

        // ---------- 복구/생성 ----------
        private static void Repair(StringBuilder log)
        {
            Sprite onigiri = AssetDatabase.LoadAllAssetsAtPath(HeartSheetPath).OfType<Sprite>()
                .FirstOrDefault(s => s.name == "hp_onigiri_0");
            if (onigiri == null)
            {
                log.AppendLine($"  ERROR: {HeartSheetPath} 에서 hp_onigiri_0 스프라이트를 찾을 수 없습니다");
                return;
            }

            HealthPickup pickup = CreateOrUpdatePickupPrefab(onigiri, log);
            if (pickup == null) return;

            foreach (string path in EnemyPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var dropper = contents.GetComponent<LootDropper>();
                    if (dropper == null) dropper = contents.AddComponent<LootDropper>();
                    dropper.dropPrefab = pickup;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    log.AppendLine($"  {path}: LootDropper linked");
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }

        private static HealthPickup CreateOrUpdatePickupPrefab(Sprite sprite, StringBuilder log)
        {
            GameObject saved;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) == null)
            {
                var go = new GameObject("Pickup_Onigiri");
                try
                {
                    ConfigurePickup(go, sprite);
                    saved = PrefabUtility.SaveAsPrefabAsset(go, PickupPath);
                    log.AppendLine($"  {PickupPath}: created");
                }
                finally { Object.DestroyImmediate(go); }
            }
            else
            {
                var contents = PrefabUtility.LoadPrefabContents(PickupPath);
                try
                {
                    ConfigurePickup(contents, sprite);
                    saved = PrefabUtility.SaveAsPrefabAsset(contents, PickupPath);
                    log.AppendLine($"  {PickupPath}: updated");
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            return saved != null ? saved.GetComponent<HealthPickup>() : null;
        }

        /// <summary>주먹밥 아이템 구성. 콜라이더를 HealthPickup보다 먼저 붙입니다 (RequireComponent가 추상 Collider2D라서).</summary>
        private static void ConfigurePickup(GameObject go, Sprite sprite)
        {
            go.layer = 0;
            go.transform.localScale = Vector3.one;

            var sr = GetOrAdd<SpriteRenderer>(go);
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = PickupSortingOrder;

            var rb = GetOrAdd<Rigidbody2D>(go);
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var col = GetOrAdd<BoxCollider2D>(go);
            col.isTrigger = true;
            col.size = (Vector2)sprite.bounds.size * 1.3f; // 그림보다 넉넉하게 → 스치기만 해도 먹힘
            col.offset = Vector2.zero;

            var pickup = GetOrAdd<HealthPickup>(go);
            var so = new SerializedObject(pickup);
            so.FindProperty("groundLayer").intValue = 1 << LayerMask.NameToLayer("Ground");
            so.ApplyModifiedPropertiesWithoutUndo();

            GetOrAdd<DespawnBehind>(go);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out T c) ? c : go.AddComponent<T>();

        // ---------- 플레이 중 테스트 메뉴 ----------
        [MenuItem("Tools/Samurai Runner/테스트/화면의 궁수·닌자 모두 처치 (Play 중)")]
        private static void TestKillAllEnemies()
        {
            var enemies = Object.FindObjectsByType<ThrowerEnemy>(FindObjectsInactive.Exclude);
            int n = 0;
            foreach (var e in enemies) { if (!e.IsDead) { e.Kill(); n++; } }
            Debug.Log($"[테스트] 적 {n}명 처치 → 주먹밥 드롭 확인");
        }

        [MenuItem("Tools/Samurai Runner/테스트/화면의 궁수·닌자 모두 처치 (Play 중)", true)]
        private static bool TestKillAllEnemiesValidate() => EditorApplication.isPlaying;

        [MenuItem("Tools/Samurai Runner/테스트/플레이어 체력 1 감소 (Play 중)")]
        private static void TestDamagePlayer()
        {
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            if (health != null && health.TakeDamage(1))
                Debug.Log($"[테스트] 체력 {health.CurrentHearts}/{health.MaxHearts} → 주먹밥을 먹으면 회복");
        }

        [MenuItem("Tools/Samurai Runner/테스트/플레이어 체력 1 감소 (Play 중)", true)]
        private static bool TestDamagePlayerValidate() => EditorApplication.isPlaying;
    }
}
