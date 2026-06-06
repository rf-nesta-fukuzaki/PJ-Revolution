using UnityEngine;

namespace Sandbox.World.Config
{
    /// <summary>
    /// オブジェクト配置 (scatter) パラメータ (Module 3)。
    /// Tree(prototype 0) は Grass/Forest、Rock(prototype 1) は Rock バイオームに配置。
    /// </summary>
    [CreateAssetMenu(menuName = "PJ-Revolution/World/Placement Params", fileName = "PlacementParams")]
    public sealed class PlacementParams : ScriptableObject
    {
        [Header("Candidate grid")]
        [Tooltip("チャンク内部のジッタ候補格子の 1 辺。候補数 = dim^2。maxInstances を超えないこと。")]
        [Range(8, 128)] public int candidateGridDim = 64;

        [Header("Tree (prototype 0) — Grass / Forest")]
        [Range(0f, 1f)] public float treeDensity = 0.3f;
        [Range(0f, 90f)] public float treeMaxSlopeDeg = 28f;
        [Min(0.01f)] public float treeScaleMin = 1.0f;
        [Min(0.01f)] public float treeScaleMax = 2.5f;

        [Header("Rock (prototype 1) — Rock biome")]
        [Range(0f, 1f)] public float rockDensity = 0.25f;
        [Min(0.01f)] public float rockScaleMin = 0.5f;
        [Min(0.01f)] public float rockScaleMax = 1.8f;

        [Header("Sea-level cutoff")]
        [Tooltip("このワールドY[m]未満には木/岩を配置しない。海面プレーンは seaLevel(0)+waterLevelOffset(4)=y4 に描画され、" +
                 "砂浜は水際〜y12付近の帯。植生がその砂浜に立たないよう、砂浜の上端より十分上(=24m)を既定にして" +
                 "『砂浜＝木無し、その上から草地＋森』の自然な渚を残す。")]
        public float minPlacementAltitude = 24f;
    }
}
