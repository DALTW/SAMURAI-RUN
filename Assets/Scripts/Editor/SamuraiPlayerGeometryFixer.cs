using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SamuraiRunner.Player;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 플레이어 지오메트리 보정: 스프라이트가 프레임 전체(96px)가 아니라
    /// 캐릭터 몸에 딱 맞게 잘려 있으므로(약 22x36px, 피벗=중앙),
    /// 실제 스프라이트 bounds를 기준으로 콜라이더·접지 판정·히트박스·위치를 재계산합니다.
    /// </summary>
    public static class SamuraiPlayerGeometryFixer
    {
        private const float GroundTopY = -1.5f;

        [MenuItem("Tools/Samurai Runner/플레이어 지오메트리 보정")]
        public static void Fix()
        {
            var player = GameObject.Find("Player");
            var sr = player != null ? player.GetComponent<SpriteRenderer>() : null;
            if (player == null || sr == null || sr.sprite == null)
            {
                Debug.LogError("[SamuraiPlayerGeometryFixer] Player 또는 스프라이트를 찾을 수 없습니다.");
                return;
            }

            ApplyGeometry(player, sr.sprite);
            ThickenGround();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SamuraiPlayerGeometryFixer] 보정 완료 — 발이 지면에 닿습니다.");
        }

        /// <summary>스프라이트 실제 크기에 맞춰 플레이어의 물리 값들을 계산합니다. (씬 구성기도 호출)</summary>
        public static void ApplyGeometry(GameObject player, Sprite reference)
        {
            Bounds b = reference.bounds; // 잘린 스프라이트 기준 (피벗=중앙이면 center≈0)

            // 발(스프라이트 아래끝)이 바닥 윗면에 닿는 높이에서 시작
            player.transform.position = new Vector3(
                player.transform.position.x,
                GroundTopY + b.extents.y - b.center.y + 0.02f,
                0f);

            var col = player.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = new Vector2(b.size.x * 0.85f, b.size.y);
                col.offset = b.center;
            }

            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null)
            {
                var so = new SerializedObject(motor);
                so.FindProperty("groundCheckOffset").vector2Value =
                    new Vector2(b.center.x, b.center.y - b.extents.y - 0.03f);
                so.FindProperty("groundCheckSize").vector2Value =
                    new Vector2(b.size.x * 0.7f, 0.12f);
                so.ApplyModifiedProperties();
            }

            var attack = player.GetComponent<PlayerAttack>();
            if (attack != null)
            {
                var so = new SerializedObject(attack);
                so.FindProperty("hitboxOffset").vector2Value =
                    new Vector2(b.extents.x + 0.7f, b.center.y);
                so.FindProperty("hitboxSize").vector2Value =
                    new Vector2(1.4f, Mathf.Max(1.1f, b.size.y));
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>바닥 띠를 아래로 두껍게 만들어 화면 하단에 배경색 틈이 보이지 않게 합니다.</summary>
        private static void ThickenGround()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) return;

            var sr = ground.GetComponent<SpriteRenderer>();
            var col = ground.GetComponent<BoxCollider2D>();
            if (sr == null || sr.drawMode == SpriteDrawMode.Simple) return;

            float width = sr.size.x;
            ground.transform.position = new Vector3(ground.transform.position.x, GroundTopY - 1f, 0f);
            ground.transform.localScale = Vector3.one;
            sr.size = new Vector2(width, 2f);
            if (col != null) { col.size = new Vector2(width, 2f); col.offset = Vector2.zero; }
        }
    }
}
