using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// ランタイムのフレームレート/同期/品質まわりを 1 箇所で設定する軽量コンポーネント。
    /// 地形生成のフレーム予算（TerrainGenerator 側 perFrameDispatchBudget）は触らず、
    /// アプリ全体の設定だけを責務として持つ（非破壊）。
    /// SandboxBootstrap が AddComponent。
    /// </summary>
    [DefaultExecutionOrder(-40)] // 何より先にフレーム設定を効かせる
    public sealed class SandboxPerformanceConfig : MonoBehaviour
    {
        [Tooltip("目標フレームレート。-1 なら未設定（プラットフォーム既定）。")]
        [SerializeField] private int targetFrameRate = 60;
        [Tooltip("VSync。targetFrameRate を効かせるには 0（Off）が必要。")]
        [SerializeField] private int vSyncCount = 0;
        [Tooltip("物理の固定タイムステップ。0 以下なら変更しない。")]
        [SerializeField] private float fixedTimeStep = 0f; // 既定維持（0.02）
        [Tooltip("最大許容 deltaTime（スパイク時の物理暴走を防ぐ）。0 以下なら変更しない。")]
        [SerializeField] private float maxDeltaTime = 0.1f;

        private void OnEnable()
        {
            QualitySettings.vSyncCount = vSyncCount;
            if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
            if (fixedTimeStep > 0f) Time.fixedDeltaTime = fixedTimeStep;
            if (maxDeltaTime > 0f) Time.maximumDeltaTime = maxDeltaTime;
        }
    }
}
