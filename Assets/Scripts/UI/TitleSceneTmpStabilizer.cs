using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// TitleScene の TMP 表示を安定化するためのユーティリティ。
/// </summary>
public static class TitleSceneTmpStabilizer
{
    private const float DefaultOpaqueAtlasThreshold = 0.96f;
    private const int DefaultSamplesPerAxis = 32;

    public static TMP_FontAsset ResolveReadableFallbackFont(TMP_FontAsset preferredFontAsset)
    {
        if (IsUsableFallbackFont(preferredFontAsset))
        {
            return preferredFontAsset;
        }

        List<TMP_FontAsset> fallbackFonts = TMP_Settings.fallbackFontAssets;
        if (fallbackFonts != null)
        {
            for (int i = 0; i < fallbackFonts.Count; i++)
            {
                TMP_FontAsset fallback = fallbackFonts[i];
                if (IsUsableFallbackFont(fallback))
                {
                    return fallback;
                }
            }
        }

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (IsUsableFallbackFont(defaultFont))
        {
            return defaultFont;
        }

        if (preferredFontAsset != null)
        {
            return preferredFontAsset;
        }

        if (fallbackFonts != null)
        {
            for (int i = 0; i < fallbackFonts.Count; i++)
            {
                TMP_FontAsset fallback = fallbackFonts[i];
                if (fallback != null)
                {
                    return fallback;
                }
            }
        }

        return defaultFont;
    }

    public static int StabilizeTexts(IReadOnlyList<TMP_Text> texts, TMP_FontAsset fallbackFontAsset)
    {
        if (texts == null || texts.Count == 0 || fallbackFontAsset == null)
        {
            return 0;
        }

        Dictionary<TMP_FontAsset, bool> invalidAtlasCache = new();
        int replacedCount = 0;

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            bool shouldReplace = ShouldReplaceFont(text, invalidAtlasCache);
            if (!shouldReplace || !CanFontRenderText(fallbackFontAsset, text.text))
            {
                continue;
            }

            text.font = fallbackFontAsset;
            if (fallbackFontAsset.material != null)
            {
                text.fontSharedMaterial = fallbackFontAsset.material;
            }

            text.havePropertiesChanged = true;
            text.SetAllDirty();
            replacedCount++;
        }

        return replacedCount;
    }

    public static bool IsFontAtlasLikelyInvalid(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return true;
        }

        Texture2D atlasTexture = fontAsset.atlasTexture;
        if (atlasTexture == null)
        {
            return true;
        }

        if (!atlasTexture.isReadable)
        {
            return false;
        }

        return IsAtlasOverlyOpaque(atlasTexture);
    }

    public static bool IsAtlasOverlyOpaque(
        Texture2D atlasTexture,
        float opaqueRatioThreshold = DefaultOpaqueAtlasThreshold,
        int samplesPerAxis = DefaultSamplesPerAxis)
    {
        if (atlasTexture == null)
        {
            return true;
        }

        int sampleCountPerAxis = Mathf.Clamp(samplesPerAxis, 4, 128);
        float threshold = Mathf.Clamp01(opaqueRatioThreshold);
        int opaqueSamples = 0;
        int totalSamples = sampleCountPerAxis * sampleCountPerAxis;

        for (int y = 0; y < sampleCountPerAxis; y++)
        {
            float v = (y + 0.5f) / sampleCountPerAxis;
            for (int x = 0; x < sampleCountPerAxis; x++)
            {
                float u = (x + 0.5f) / sampleCountPerAxis;
                Color sample = atlasTexture.GetPixelBilinear(u, v);
                if (sample.a >= 0.95f)
                {
                    opaqueSamples++;
                }
            }
        }

        float opaqueRatio = totalSamples > 0 ? (float)opaqueSamples / totalSamples : 1f;
        return opaqueRatio >= threshold;
    }

    private static bool IsUsableFallbackFont(TMP_FontAsset fontAsset)
    {
        return fontAsset != null && !IsFontAtlasLikelyInvalid(fontAsset);
    }

    private static bool ShouldReplaceFont(TMP_Text text, IDictionary<TMP_FontAsset, bool> invalidAtlasCache)
    {
        TMP_FontAsset fontAsset = text.font;
        if (fontAsset == null)
        {
            return true;
        }

        if (!invalidAtlasCache.TryGetValue(fontAsset, out bool atlasInvalid))
        {
            atlasInvalid = IsFontAtlasLikelyInvalid(fontAsset);
            invalidAtlasCache[fontAsset] = atlasInvalid;
        }

        if (atlasInvalid)
        {
            return true;
        }

        return !CanFontRenderText(fontAsset, text.text);
    }

    private static bool CanFontRenderText(TMP_FontAsset fontAsset, string text)
    {
        if (fontAsset == null || string.IsNullOrEmpty(text))
        {
            return true;
        }

        bool inRichTextTag = false;
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (character == '<')
            {
                inRichTextTag = true;
                continue;
            }

            if (character == '>' && inRichTextTag)
            {
                inRichTextTag = false;
                continue;
            }

            if (inRichTextTag || char.IsWhiteSpace(character) || char.IsControl(character))
            {
                continue;
            }

            if (!fontAsset.HasCharacter(character, true, true))
            {
                return false;
            }
        }

        return true;
    }
}
