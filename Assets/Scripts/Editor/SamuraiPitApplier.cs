using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.Environment;
using SamuraiRunner.Player;
using SamuraiRunner.Spawning;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 7단계: 낙사 구덩이 추가 에디터 스크립트.
    /// Tools > Samurai Runner > 7단계 - 구덩이(낙사) 추가 를 실행하면:
    ///   1) 이전 단계(1~6)가 없으면 먼저 구성
    ///   2) Ground에 GroundGenerator를 붙임 (플레이 중 Ground를 견본으로 바닥 조각 + 구덩이를 계속 생성)
    ///   3) Spawner가 GroundGenerator를 보도록 연결 (구덩이 근처에는 장애물·적을 만들지 않음)
    /// 처음 한 번은 스크립트 로드 시 자동으로 "검증"만 하고, 빠진 것이 있을 때만 2)~3)을 실행합니다
    /// (Library/SamuraiPitSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiPitSetup.report.txt).
    /// 구덩이 폭·간격·낙사 높이는 Ground / Spawner / Player의 Inspector에서 조절합니다.
    /// </summary>
    public static class SamuraiPitApplier
    {
        private const string MarkerPath = "Library/SamuraiPitSetup.done";
        private const string ReportPath = "Library/SamuraiPitSetup.report.txt";
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

        [MenuItem("Tools/Samurai Runner/7단계 - 구덩이(낙사) 추가")]
        public static void Apply()
        {
            SamuraiPickupApplier.Apply();
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
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiPitApplier auto={auto} force={forceRepair}");
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
                    if (Repair(log))
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
                if (ok) Debug.Log($"[SamuraiPitApplier] 구덩이 설정 확인 완료. 리포트: {ReportPath}");
                else Debug.LogWarning($"[SamuraiPitApplier] 구덩이 설정을 확인하지 못했습니다. 리포트: {ReportPath}");
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
            var groundGO = GameObject.Find("Ground");
            if (groundGO == null) { log.AppendLine("  Ground: NOT FOUND in active scene"); return false; }

            var gen = groundGO.GetComponent<GroundGenerator>();
            var sr = groundGO.GetComponent<SpriteRenderer>();
            var col = groundGO.GetComponent<BoxCollider2D>();
            float top = col != null ? groundGO.transform.position.y + col.offset.y + col.size.y * 0.5f : float.NaN;
            log.AppendLine($"  Ground: generator={(gen != null)}, template sprite={(sr != null && sr.sprite != null ? sr.sprite.name : "NULL")}, " +
                           $"drawMode={(sr != null ? sr.drawMode.ToString() : "-")}, collider={(col != null)}, groundTopY={top}, layer={LayerMask.LayerToName(groundGO.layer)}");
            if (gen == null || col == null || sr == null || sr.sprite == null) ok = false;

            Vector2 pitWidth = Vector2.zero;
            if (gen != null)
            {
                var so = new SerializedObject(gen);
                var playerRef = so.FindProperty("player").objectReferenceValue;
                pitWidth = so.FindProperty("pitWidthRange").vector2Value;
                Vector2 groundLen = so.FindProperty("groundLengthRange").vector2Value;
                log.AppendLine($"  GroundGenerator: player={(playerRef != null ? playerRef.name : "auto-find")}, pitWidth={pitWidth}, groundLength={groundLen}, " +
                               $"safeStart={so.FindProperty("safeStartDistance").floatValue}, ahead={so.FindProperty("generateAhead").floatValue}");
            }

            var spawner = Object.FindAnyObjectByType<Spawner>();
            if (spawner == null) log.AppendLine("  Spawner: not in scene (pits still work, nothing to avoid)");
            else
            {
                var sso = new SerializedObject(spawner);
                Object linked = sso.FindProperty("ground").objectReferenceValue;
                bool spawnerOk = gen != null && linked == gen;
                log.AppendLine($"  Spawner: ground={(linked != null ? linked.name : "NULL")}, linked={spawnerOk}, " +
                               $"pitClearance={sso.FindProperty("pitClearance").floatValue}, minSpawnGap={sso.FindProperty("minSpawnGap").floatValue}, " +
                               $"spawnAhead={sso.FindProperty("spawnAheadDistance").floatValue}");
                if (!spawnerOk) ok = false;
            }

            var health = Object.FindAnyObjectByType<PlayerHealth>();
            if (health == null) { log.AppendLine("  PlayerHealth: NOT FOUND"); ok = false; }
            else
            {
                var hso = new SerializedObject(health);
                log.AppendLine($"  PlayerHealth: fallDeathY={hso.FindProperty("fallDeathY").floatValue}, player y={health.transform.position.y}");
            }

            // 길게 누른 점프로 넘을 수 있는 거리 vs 구덩이 최대 폭 (참고용)
            var motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null && pitWidth.y > 0f)
            {
                var mso = new SerializedObject(motor);
                float run = mso.FindProperty("runSpeed").floatValue;
                float jump = mso.FindProperty("jumpForce").floatValue;
                float gs = mso.FindProperty("gravityScale").floatValue;
                float fall = mso.FindProperty("fallGravityMultiplier").floatValue;
                float g = Mathf.Abs(Physics2D.gravity.y) * gs;
                float tUp = jump / g;
                float h = jump * jump / (2f * g);
                float tDown = Mathf.Sqrt(2f * h / (g * fall));
                float reach = run * (tUp + tDown);
                log.AppendLine($"  jump reach (hold) ≈ {reach:0.00} units vs max pit width {pitWidth.y:0.00} → {(reach > pitWidth.y + 0.5f ? "jumpable" : "TOO WIDE")}");
                if (reach <= pitWidth.y + 0.5f) ok = false;
            }
            return ok;
        }

        // ---------- 복구/생성 ----------
        private static bool Repair(StringBuilder log)
        {
            var groundGO = GameObject.Find("Ground");
            if (groundGO == null) { log.AppendLine("  ERROR: Ground 오브젝트가 없습니다 (1단계 씬 자동 구성을 먼저 실행하세요)"); return false; }

            var gen = groundGO.GetComponent<GroundGenerator>();
            if (gen == null) { gen = groundGO.AddComponent<GroundGenerator>(); log.AppendLine("  GroundGenerator added to Ground"); }

            var player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player != null)
            {
                var so = new SerializedObject(gen);
                so.FindProperty("player").objectReferenceValue = player.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(gen);

            var spawner = Object.FindAnyObjectByType<Spawner>();
            if (spawner != null)
            {
                spawner.ground = gen;
                EditorUtility.SetDirty(spawner);
                log.AppendLine("  Spawner.ground linked");
            }
            return true;
        }
    }
}
