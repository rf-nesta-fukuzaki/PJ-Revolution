#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// アクティブシーン上の <see cref="MountainTerrainGenerator"/> を手動再生成する。
    /// </summary>
    public static class MountainTerrainGeneratorEditor
    {
        [MenuItem(PeakPlunderEditorMenus.Terrain.GenerateMountainTerrain)]
        public static void GenerateFromMenu()
        {
            var gen = Object.FindFirstObjectByType<MountainTerrainGenerator>();
            if (gen == null)
            {
                Debug.LogWarning("[MountainTerrain] シーンに MountainTerrainGenerator が見つかりません。"
                    + " World/Mountain に追加してから実行してください。");
                return;
            }

            gen.Generate();
        }
    }
}
#endif
