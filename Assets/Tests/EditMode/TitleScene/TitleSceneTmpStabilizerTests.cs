using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

public sealed class TitleSceneTmpStabilizerTests
{
    [Test]
    public void IsAtlasOverlyOpaque_ReturnsTrue_ForFullyOpaqueTexture()
    {
        Texture2D texture = CreateSolidAlphaTexture(16, 16, 1f);
        try
        {
            bool result = InvokeIsAtlasOverlyOpaque(texture, 0.9f, 8);
            Assert.That(result, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void IsAtlasOverlyOpaque_ReturnsFalse_ForMostlyTransparentTexture()
    {
        Texture2D texture = CreateSolidAlphaTexture(16, 16, 0f);
        try
        {
            texture.SetPixel(0, 0, new Color(1f, 1f, 1f, 1f));
            texture.SetPixel(15, 15, new Color(1f, 1f, 1f, 1f));
            texture.Apply(false, false);

            bool result = InvokeIsAtlasOverlyOpaque(texture, 0.9f, 8);
            Assert.That(result, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static bool InvokeIsAtlasOverlyOpaque(Texture2D texture, float threshold, int samplesPerAxis)
    {
        MethodInfo method = GetMethodOrFail("IsAtlasOverlyOpaque", typeof(Texture2D), typeof(float), typeof(int));
        object result = method.Invoke(null, new object[] { texture, threshold, samplesPerAxis });
        return result is bool value && value;
    }

    private static MethodInfo GetMethodOrFail(string methodName, params Type[] parameterTypes)
    {
        Type stabilizerType = Type.GetType("TitleSceneTmpStabilizer, Assembly-CSharp");
        Assert.That(stabilizerType, Is.Not.Null, "Type not found: TitleSceneTmpStabilizer, Assembly-CSharp");

        MethodInfo method = stabilizerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        return method;
    }

    private static Texture2D CreateSolidAlphaTexture(int width, int height, float alpha)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false, true);
        Color color = new(1f, 1f, 1f, Mathf.Clamp01(alpha));
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
