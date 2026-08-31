using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SamuraiRunner.EditorTools
{
    /// <summary>
    /// 사무라이 시트의 프레임별 피벗 통일.
    /// 이 에셋은 프레임마다 캐릭터 몸에 딱 맞게 다른 크기로 잘려 있어서(중앙 피벗),
    /// 프레임이 바뀔 때마다 그려지는 높이가 달라집니다.
    /// 모든 스프라이트의 피벗을 "원본 96px 셀의 중앙"이라는 하나의 좌표 기준으로 바꿔
    /// 어떤 프레임이든 발 위치가 일정하게 만듭니다. 이후 플레이어 지오메트리를 재계산합니다.
    /// </summary>
    public static class SamuraiSpritePivotFixer
    {
        private const string SpriteFolder = "Assets/Assets/FREE_Samurai 2D Pixel Art v1.2/Sprites";
        private const float Cell = 96f;

        [MenuItem("Tools/Samurai Runner/스프라이트 피벗 통일 + 지오메트리 재계산")]
        public static void FullFix()
        {
            FixPivots();

            // 피벗이 바뀐 스프라이트 기준으로 플레이어 물리 재계산
            var player = GameObject.Find("Player");
            Sprite idle0 = AssetDatabase.LoadAllAssetsAtPath($"{SpriteFolder}/IDLE.png")
                .OfType<Sprite>().FirstOrDefault(s => s.name == "IDLE_0");
            if (player != null && idle0 != null)
            {
                SamuraiPlayerGeometryFixer.ApplyGeometry(player, idle0);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log("[SamuraiSpritePivotFixer] 피벗 통일 + 지오메트리 재계산 완료.");
        }

        /// <summary>4개 시트의 모든 스프라이트 피벗을 96px 셀 중앙 기준 커스텀 피벗으로 설정합니다.</summary>
        public static void FixPivots()
        {
            foreach (string file in new[] { "IDLE.png", "RUN.png", "ATTACK 1.png", "HURT.png" })
            {
                string path = $"{SpriteFolder}/{file}";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

#pragma warning disable 618 // spritesheet API는 구식 표기지만 여전히 동작하며 스프라이트 ID를 유지합니다
                SpriteMetaData[] sheet = importer.spritesheet;
                if (sheet == null || sheet.Length == 0) continue;

                for (int i = 0; i < sheet.Length; i++)
                {
                    Rect r = sheet[i].rect;
                    int cell = Mathf.FloorToInt((r.x + r.width * 0.5f) / Cell);
                    float cellCenterX = cell * Cell + Cell * 0.5f;
                    float cellCenterY = Cell * 0.5f;

                    sheet[i].alignment = (int)SpriteAlignment.Custom;
                    sheet[i].pivot = new Vector2(
                        (cellCenterX - r.x) / r.width,
                        (cellCenterY - r.y) / r.height);
                }
                importer.spritesheet = sheet;
#pragma warning restore 618
                importer.SaveAndReimport();
            }
        }
    }
}
