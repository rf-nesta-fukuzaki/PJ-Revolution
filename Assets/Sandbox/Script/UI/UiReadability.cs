using TMPro;
using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>
    /// TMP テキストを「どんな 3D 背景の上でも読める」ようにする可読性ヘルパー。
    /// PEAK 風の手法: 明るい face はそのまま残し、暗いソフトシャドウ(Underlay)と
    /// 細いアウトラインでコントラストを確保する。背景パネルを足さないので
    /// ミニマリズム / 余白を損なわない。
    ///
    /// 設定値はすべて数値（オフセット・幅・α）なので、画面を直接見なくても
    /// メタデータとして決定的に検証できる。
    /// </summary>
    public static class UiReadability
    {
        // 黒に近いシャドウ/アウトラインで、白 face のコントラストを底上げする。
        public static readonly Color DefaultShadow  = new Color(0f, 0f, 0f, 0.85f);
        public static readonly Color DefaultOutline = new Color(0f, 0f, 0f, 0.9f);

        public const float DefaultOutlineWidth   = 0.15f;
        public const float DefaultShadowOffset   = 0.55f;
        public const float DefaultShadowDilate   = 0.05f;
        public const float DefaultShadowSoftness = 0.3f;

        /// <summary>
        /// face は維持したまま、アウトライン + ソフトシャドウ(Underlay)を付与してコントラストを底上げ。
        /// fontMaterial を参照するとマテリアルがインスタンス化されるため、
        /// HUD の少数ラベル向け（多用は Draw Call 増に注意）。
        /// </summary>
        public static void MakeReadable(TMP_Text text,
            float outlineWidth = DefaultOutlineWidth,
            float shadowOffset = DefaultShadowOffset,
            float shadowSoftness = DefaultShadowSoftness)
        {
            if (text == null) return;

            // フォント未割当のまま fontMaterial を読むと内部で new Material(null) となり
            // ArgumentNullException を投げる。既定フォントで補完し、無ければ安全に抜ける。
            if (text.font == null)
            {
                if (TMP_Settings.defaultFontAsset == null) return;
                text.font = TMP_Settings.defaultFontAsset;
            }
            if (text.font.material == null) return;

            // per-instance material（共有マテリアルを汚染しない）
            Material mat = text.fontMaterial;
            if (mat == null) return;

            // アウトライン（距離フィールドシェーダ標準・キーワード不要）
            mat.SetColor(ShaderUtilities.ID_OutlineColor, DefaultOutline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

            // ソフトシャドウ（Underlay）
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, DefaultShadow);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shadowOffset);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, DefaultShadowDilate);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
        }
    }
}
