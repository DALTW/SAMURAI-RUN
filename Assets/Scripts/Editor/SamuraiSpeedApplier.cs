using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SamuraiRunner.Player;
using SamuraiRunner.Systems;
using SamuraiRunner.UI;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 9단계: 거리에 따른 속도 증가 + 게임오버 기록(달린 거리·최대 콤보) 에디터 스크립트.
    /// Tools > Samurai Runner > 9단계 - 속도 증가·게임오버 기록 을 실행하면:
    ///   1) 이전 단계(1~8)가 없으면 먼저 구성
    ///   2) GameSystems에 SpeedRamp(50m마다 달리기 속도 +0.5, 최대 9)를 붙이고 연결
    ///   3) GameHUD가 게임오버 텍스트에 기록을 쓰도록 연결하고, 기록 줄이 들어가게 텍스트 상자를 키움
    /// 처음 한 번은 스크립트 로드 시 자동으로 "검증"만 하고, 빠진 것이 있을 때만 2)~3)을 실행합니다
    /// (Library/SamuraiSpeedSetup.done 표시 파일로 1회 제한, 결과는 Library/SamuraiSpeedSetup.report.txt).
    /// </summary>
    public static class SamuraiSpeedApplier
    {
        private const string MarkerPath = "Library/SamuraiSpeedSetup.done";
        private const string ReportPath = "Library/SamuraiSpeedSetup.report.txt";
        private const float GameOverHeight = 320f;
        private const double SettleSeconds = 3.0;
        private static double idleSince = -1;

        // 게임오버 문구 (영어). {0} = 달린 거리(m), {1} = 최대 콤보
        private const string EnglishFormat = "The Samurai's Run Has Ended\n\nDistance  {0} m\nMax Combo  {1}\n\nR - Run Again";
        private const string EnglishIdleText = "The Samurai's Run Has Ended\n\nR - Run Again";

        /// <summary>한글 음절이 들어 있는지 (예전 한국어 문구가 남아 있는지 확인용)</summary>
        private static bool HasHangul(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= '가' && c <= '힣') return true;
            return false;
        }

        [MenuItem("Tools/Samurai Runner/9단계 - 속도 증가·게임오버 기록")]
        public static void Apply()
        {
            SamuraiSwingLogApplier.Apply();
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
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] SamuraiSpeedApplier auto={auto} force={forceRepair}");
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
                if (ok) Debug.Log($"[SamuraiSpeedApplier] 속도 증가·게임오버 기록 설정 확인 완료. 리포트: {ReportPath}");
                else Debug.LogWarning($"[SamuraiSpeedApplier] 속도 증가·게임오버 기록 설정을 확인하지 못했습니다. 리포트: {ReportPath}");
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

            var ramp = Object.FindAnyObjectByType<SpeedRamp>();
            var motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (ramp == null) { log.AppendLine("  SpeedRamp: NOT FOUND"); ok = false; }
            else
            {
                var so = new SerializedObject(ramp);
                float perMeters = so.FindProperty("metersPerStep").floatValue;
                float step = so.FindProperty("speedPerStep").floatValue;
                float max = so.FindProperty("maxSpeed").floatValue;
                float accel = so.FindProperty("acceleration").floatValue;
                log.AppendLine($"  SpeedRamp on '{ramp.gameObject.name}': motor={(ramp.playerMotor != null ? "linked" : "auto-find")}, " +
                               $"distance={(ramp.distanceTracker != null ? "linked" : "auto-find")}, every {perMeters}m +{step}, max {max}, accel {accel}");

                if (motor != null && perMeters > 0f && step > 0f)
                {
                    float baseSpeed = new SerializedObject(motor).FindProperty("runSpeed").floatValue;
                    // 거리별 속도표 + 최고 속도까지 걸리는 시간
                    var table = new StringBuilder("  speed by distance:");
                    float t = 0f, speed = baseSpeed;
                    for (int level = 0; ; level++)
                    {
                        float target = Mathf.Min(max, baseSpeed + level * step);
                        table.Append($" {level * perMeters:0}m→{target:0.0}");
                        if (target >= max || level > 40) break;
                        t += perMeters / Mathf.Max(0.1f, (speed + target) * 0.5f); // 대략적인 평균 속도로 구간 시간
                        speed = target;
                    }
                    log.AppendLine(table.ToString());
                    log.AppendLine($"  base speed {baseSpeed} → max {max} reached after ≈{t:0}s");

                    // 최고 속도에서 길게 누른 점프 비거리 (구덩이 최대 폭과 비교용)
                    var mso = new SerializedObject(motor);
                    float jump = mso.FindProperty("jumpForce").floatValue;
                    float gs = mso.FindProperty("gravityScale").floatValue;
                    float fall = mso.FindProperty("fallGravityMultiplier").floatValue;
                    float g = Mathf.Abs(Physics2D.gravity.y) * gs;
                    float h = jump * jump / (2f * g);
                    float air = jump / g + Mathf.Sqrt(2f * h / (g * fall));
                    log.AppendLine($"  jump reach (hold): {baseSpeed * air:0.00} units at start, {max * air:0.00} units at max speed");
                }
            }

            var hud = Object.FindAnyObjectByType<GameHUD>();
            var flow = Object.FindAnyObjectByType<GameFlowController>();
            Text expected = flow != null && flow.gameOverUI != null ? flow.gameOverUI.GetComponentInChildren<Text>(true) : null;
            if (hud == null) { log.AppendLine("  GameHUD: NOT FOUND"); ok = false; }
            else
            {
                bool linked = hud.gameOverText != null && (expected == null || hud.gameOverText == expected);
                log.AppendLine($"  GameHUD.gameOverText={(hud.gameOverText != null ? hud.gameOverText.name : "NULL (auto-find at death)")}, matches GameOver UI={linked}");
                if (hud.gameOverText == null && expected == null) ok = false;

                string format = new SerializedObject(hud).FindProperty("gameOverFormat").stringValue;
                string sample;
                try { sample = string.Format(format, 123, 7); } catch (System.FormatException) { sample = "(format error)"; ok = false; }
                log.AppendLine("  game over text sample (123 m, combo 7): " + sample.Replace("\n", " / "));
                if (HasHangul(format)) { log.AppendLine("  game over format is still Korean → should be English"); ok = false; }
            }
            if (expected != null)
            {
                float height = expected.rectTransform.sizeDelta.y;
                log.AppendLine($"  GameOver text box height={height} (needs ≥ 300 for 6 lines at font {expected.fontSize})");
                if (height < 300f) ok = false;
                log.AppendLine("  GameOver idle text: " + expected.text.Replace("\n", " / "));
                if (HasHangul(expected.text)) { log.AppendLine("  GameOver idle text is still Korean → should be English"); ok = false; }
            }
            return ok;
        }

        // ---------- 복구/생성 ----------
        /// <returns>씬을 바꿨으면 true</returns>
        private static bool Repair(StringBuilder log)
        {
            bool changed = false;

            var systems = GameObject.Find("GameSystems");
            var motor = Object.FindAnyObjectByType<PlayerMotor>();
            var distance = Object.FindAnyObjectByType<DistanceTracker>();
            if (systems == null) log.AppendLine("  ERROR: GameSystems 오브젝트가 없습니다 (2단계 전투 요소 추가를 먼저 실행하세요)");
            else
            {
                var ramp = systems.GetComponent<SpeedRamp>();
                if (ramp == null) { ramp = systems.AddComponent<SpeedRamp>(); log.AppendLine("  SpeedRamp added to GameSystems"); }
                ramp.playerMotor = motor;
                ramp.distanceTracker = distance;
                EditorUtility.SetDirty(ramp);
                changed = true;
            }

            var hud = Object.FindAnyObjectByType<GameHUD>();
            var flow = Object.FindAnyObjectByType<GameFlowController>();
            Text text = flow != null && flow.gameOverUI != null ? flow.gameOverUI.GetComponentInChildren<Text>(true) : null;
            if (hud != null && text != null)
            {
                hud.gameOverText = text;
                EditorUtility.SetDirty(hud);
                var rt = text.rectTransform;
                if (rt.sizeDelta.y < GameOverHeight)
                {
                    rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 720f), GameOverHeight);
                    EditorUtility.SetDirty(rt);
                }
                log.AppendLine("  GameHUD.gameOverText linked, text box enlarged");
                changed = true;
            }

            // 게임오버 문구를 영어로 (한글이 남아 있을 때만 — 직접 고친 영어 문구는 그대로 둠)
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                var format = so.FindProperty("gameOverFormat");
                if (HasHangul(format.stringValue))
                {
                    format.stringValue = EnglishFormat;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("  game over format → English");
                    changed = true;
                }
            }
            if (text != null && HasHangul(text.text))
            {
                text.text = EnglishIdleText;
                EditorUtility.SetDirty(text);
                log.AppendLine("  GameOver idle text → English");
                changed = true;
            }
            return changed;
        }
    }
}
