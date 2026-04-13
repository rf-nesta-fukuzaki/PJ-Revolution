using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TitleSceneWarningRegressionTests
{
    private const string TitleScenePath = "Assets/Scenes/TitleScece.unity";
    private const string PreferredTitleFontPath = "Assets/UI/Title/Fonts/TitleRef_RoundedBold SDF.asset";
    private const string NotoFallbackFontPath = "Assets/UI/Title/NotoSansJP_Rebuilt SDF.asset";

    [Test]
    public void ResolveReadableFallbackFont_ReturnsHealthyPreferredFont()
    {
        TMP_FontAsset preferred = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PreferredTitleFontPath);
        Assert.That(preferred, Is.Not.Null, $"Missing font asset: {PreferredTitleFontPath}");
        Assert.That(InvokeIsFontAtlasLikelyInvalid(preferred), Is.False, "Precondition failed: title font should be valid atlas.");

        TMP_FontAsset resolved = InvokeResolveReadableFallbackFont(preferred);
        Assert.That(resolved, Is.Not.Null, "Resolved fallback font is null.");
        Assert.That(resolved, Is.SameAs(preferred), "Resolved fallback should keep the healthy preferred font.");
        Assert.That(InvokeIsFontAtlasLikelyInvalid(resolved), Is.False, "Resolved preferred font should remain valid.");
    }

    [Test]
    public void ResolveReadableFallbackFont_WhenPreferredIsNull_ReturnsValidFallback()
    {
        TMP_FontAsset notoFallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoFallbackFontPath);
        Assert.That(notoFallback, Is.Not.Null, $"Missing fallback font asset: {NotoFallbackFontPath}");

        TMP_FontAsset resolved = InvokeResolveReadableFallbackFont(null);
        Assert.That(resolved, Is.Not.Null, "Resolved fallback should not be null when preferred is null.");
        Assert.That(InvokeIsFontAtlasLikelyInvalid(resolved), Is.False, "Resolved fallback should have a valid atlas.");
    }

    [Test]
    public void TitleScene_ControllerReadableFallbackFont_HasValidAtlas()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            MonoBehaviour controller = FindControllerInTitleScene();
            Assert.That(controller, Is.Not.Null, "CozyCaveTitleController not found in title scene.");

            SerializedObject serialized = new(controller);
            SerializedProperty fallbackProperty = serialized.FindProperty("readableFallbackFontAsset");
            Assert.That(fallbackProperty, Is.Not.Null, "readableFallbackFontAsset property not found.");
            Assert.That(fallbackProperty.objectReferenceValue, Is.Not.Null, "readableFallbackFontAsset is not assigned.");

            TMP_FontAsset fallbackFont = fallbackProperty.objectReferenceValue as TMP_FontAsset;
            Assert.That(fallbackFont, Is.Not.Null, "readableFallbackFontAsset must be a TMP_FontAsset.");
            TMP_FontAsset preferred = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PreferredTitleFontPath);
            Assert.That(preferred, Is.Not.Null, $"Missing preferred title font: {PreferredTitleFontPath}");
            Assert.That(fallbackFont, Is.SameAs(preferred), "readableFallbackFontAsset should point to the rebuilt title font.");
            Assert.That(InvokeIsFontAtlasLikelyInvalid(fallbackFont), Is.False, "readableFallbackFontAsset must point to a valid atlas.");
        }
        finally
        {
            if (previous != null && previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    private static MonoBehaviour FindControllerInTitleScene()
    {
        return Resources
            .FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(mb =>
                mb != null &&
                mb.gameObject.scene.path == TitleScenePath &&
                mb.GetType().Name == "CozyCaveTitleController");
    }

    private static TMP_FontAsset InvokeResolveReadableFallbackFont(TMP_FontAsset preferredFontAsset)
    {
        MethodInfo method = GetStabilizerMethod("ResolveReadableFallbackFont", typeof(TMP_FontAsset));
        object resolved = method.Invoke(null, new object[] { preferredFontAsset });
        return resolved as TMP_FontAsset;
    }

    private static bool InvokeIsFontAtlasLikelyInvalid(TMP_FontAsset fontAsset)
    {
        MethodInfo method = GetStabilizerMethod("IsFontAtlasLikelyInvalid", typeof(TMP_FontAsset));
        object result = method.Invoke(null, new object[] { fontAsset });
        return result is bool invalid && invalid;
    }

    private static MethodInfo GetStabilizerMethod(string methodName, params Type[] parameterTypes)
    {
        Type stabilizerType = GetTypeOrFail("TitleSceneTmpStabilizer, Assembly-CSharp");
        MethodInfo method = stabilizerType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            parameterTypes,
            null);

        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        return method;
    }

    private static Type GetTypeOrFail(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName);
        Assert.That(type, Is.Not.Null, $"Type not found: {assemblyQualifiedName}");
        return type;
    }
}
