using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>
    /// PEAK 寄りの「暖色・低彩度・スタイライズド」UI カラーパレット。
    /// 全 UI でこのトークンを共有し、配色の一貫性（criterion 3）を担保する。
    /// 個々のコンポーネントで色を直書きせず、ここを参照する。
    /// </summary>
    public static class UiPalette
    {
        // 前景・テキスト
        public static readonly Color Cream    = new Color(0.96f, 0.94f, 0.88f, 1f); // 明るい前景/テキスト
        public static readonly Color CreamDim = new Color(0.74f, 0.71f, 0.64f, 1f); // 副次テキスト（暖色グレー）
        public static readonly Color Ink      = new Color(0.10f, 0.09f, 0.11f, 1f); // 影・縁取り・暗部

        // ステータス
        public static readonly Color Sage  = new Color(0.56f, 0.82f, 0.46f, 0.95f); // 満（スタミナ等）
        public static readonly Color Coral = new Color(0.94f, 0.44f, 0.30f, 0.98f); // 低・警告
        public static readonly Color Amber = new Color(0.98f, 0.78f, 0.36f, 1f);    // アクセント/強調

        // サーフェス
        public static readonly Color PanelBg = new Color(0.07f, 0.07f, 0.09f, 0.55f); // パネル背景（半透明）
        public static readonly Color Track   = new Color(0.04f, 0.05f, 0.07f, 0.55f); // ゲージのトラック
    }
}
