using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleSceneEditModeTests
{
    private const string TitleScenePath = "Assets/Scenes/TitleScece.unity";
    private const string ConfigAssetPath = "Assets/Resources/Title/DefaultTitleSceneConfig.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string TitleRoundedFontPath = "Assets/UI/Title/Fonts/TitleRef_RoundedBold SDF.asset";
    private const string NotoFallbackFontPath = "Assets/UI/Title/NotoSansJP_Rebuilt SDF.asset";
    private const string ButtonBoardTexturePath = "Assets/UI/Title/ButtonBoard_Source_FixedOpaque.png";
    private const string ControllerTypeName = "CozyCaveTitleController";

    [Test]
    public void TitleConfig_StartSceneName_IsInBuildSettings()
    {
        Object config = AssetDatabase.LoadMainAssetAtPath(ConfigAssetPath);
        Assert.That(config, Is.Not.Null, $"Missing config asset: {ConfigAssetPath}");

        string startSceneName = ReadStringField(config, "startSceneName");
        Assert.That(startSceneName, Is.Not.Empty, "startSceneName is empty.");
        Assert.That(startSceneName, Is.EqualTo("TestScene"), "startSceneName should target TestScene.");

        bool existsInBuild = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Any(s => string.Equals(
                Path.GetFileNameWithoutExtension(s.path),
                startSceneName,
                System.StringComparison.Ordinal));

        Assert.That(existsInBuild, Is.True, $"Scene '{startSceneName}' is not in Build Settings.");
    }

    [Test]
    public void TitleScene_MenuEntries_AreFullyBound_AndCoverAllActions()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            MonoBehaviour controller = FindControllerInTitleScene();
            Assert.That(controller, Is.Not.Null, $"{ControllerTypeName} was not found in Title scene.");

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty titleConfigProp = so.FindProperty("titleConfig");
            Assert.That(titleConfigProp, Is.Not.Null, "titleConfig property not found.");
            Assert.That(titleConfigProp.objectReferenceValue, Is.Not.Null, "titleConfig is not assigned.");

            SerializedProperty entries = so.FindProperty("menuEntries");
            Assert.That(entries, Is.Not.Null, "menuEntries property not found.");
            Assert.That(entries.arraySize, Is.GreaterThanOrEqualTo(4), "menuEntries should include all 4 actions.");

            HashSet<int> actions = new();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty button = entry.FindPropertyRelative("button");
                SerializedProperty visualRoot = entry.FindPropertyRelative("visualRoot");
                SerializedProperty label = entry.FindPropertyRelative("label");
                SerializedProperty canvasGroup = entry.FindPropertyRelative("canvasGroup");
                SerializedProperty buttonFx = entry.FindPropertyRelative("buttonFx");
                SerializedProperty action = entry.FindPropertyRelative("action");

                Assert.That(button.objectReferenceValue, Is.Not.Null, $"Entry[{i}] button is null.");
                Assert.That(visualRoot.objectReferenceValue, Is.Not.Null, $"Entry[{i}] visualRoot is null.");
                Assert.That(label.objectReferenceValue, Is.Not.Null, $"Entry[{i}] label is null.");
                Assert.That(canvasGroup.objectReferenceValue, Is.Not.Null, $"Entry[{i}] canvasGroup is null.");
                Assert.That(buttonFx.objectReferenceValue, Is.Not.Null, $"Entry[{i}] buttonFx is null.");
                actions.Add(action.enumValueIndex);
            }

            Assert.That(actions.Contains(0), Is.True, "StartGame action is missing.");
            Assert.That(actions.Contains(1), Is.True, "Settings action is missing.");
            Assert.That(actions.Contains(2), Is.True, "Credits action is missing.");
            Assert.That(actions.Contains(3), Is.True, "Exit action is missing.");
        }
        finally
        {
            if (previous != null && previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    [Test]
    public void TitleButtons_UseSlicedImage_WithNonZeroSpriteBorder()
    {
        string[] buttonNames = { "Btn_StartGame", "Btn_Settings", "Btn_Credits", "Btn_Exit" };
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            foreach (string buttonName in buttonNames)
            {
                GameObject buttonObject = FindInTitleScene(buttonName);
                Assert.That(buttonObject, Is.Not.Null, $"Button object not found: {buttonName}");

                Image image = buttonObject.GetComponent<Image>();
                Assert.That(image, Is.Not.Null, $"Image component missing on '{buttonName}'.");
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), $"'{buttonName}' should use sliced image rendering.");
                Assert.That(image.sprite, Is.Not.Null, $"'{buttonName}' sprite is not assigned.");
                Assert.That(image.sprite.border.sqrMagnitude, Is.GreaterThan(0f), $"'{buttonName}' sprite border should be configured for slicing.");
            }
        }
        finally
        {
            if (previous != null && previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    [Test]
    public void TitleButtonBoardTexture_UsesUiFriendlyImportSettings()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ButtonBoardTexturePath) as TextureImporter;
        Assert.That(importer, Is.Not.Null, $"TextureImporter was not found: {ButtonBoardTexturePath}");

        Assert.That(importer.alphaIsTransparency, Is.True, "Button board texture should keep alpha transparency.");
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), "Button board texture filter mode should be bilinear.");
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "Button board texture must be uncompressed to avoid edge artifacts.");

        SerializedObject serializedImporter = new(importer);
        SerializedProperty sprites = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");
        Assert.That(sprites, Is.Not.Null, "Sprite metadata list was not found on button board importer.");
        Assert.That(sprites.arraySize, Is.GreaterThanOrEqualTo(3), "Button board sprite sheet should contain at least 3 sprites.");

        for (int i = 0; i < sprites.arraySize; i++)
        {
            SerializedProperty sprite = sprites.GetArrayElementAtIndex(i);
            SerializedProperty name = sprite.FindPropertyRelative("m_Name");
            SerializedProperty border = sprite.FindPropertyRelative("m_Border");

            Assert.That(name, Is.Not.Null, $"Sprite[{i}] name metadata is missing.");
            Assert.That(border, Is.Not.Null, $"Sprite[{i}] border metadata is missing.");
            Assert.That(border.vector4Value.sqrMagnitude, Is.GreaterThan(0f), $"Sprite '{name.stringValue}' should have non-zero border for sliced rendering.");
        }
    }

    [Test]
    public void TitleButtonBoardTexture_PlatformOverrides_AreTunedForDesktopAndAndroid()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ButtonBoardTexturePath) as TextureImporter;
        Assert.That(importer, Is.Not.Null, $"TextureImporter was not found: {ButtonBoardTexturePath}");

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");

        Assert.That(standalone.overridden, Is.True, "Standalone override should be enabled.");
        Assert.That(standalone.maxTextureSize, Is.EqualTo(4096), "Standalone max texture size should preserve source detail.");
        Assert.That(standalone.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "Standalone should keep uncompressed texture for crisp UI edges.");

        Assert.That(android.overridden, Is.True, "Android override should be enabled.");
        Assert.That(android.maxTextureSize, Is.EqualTo(2048), "Android max texture size should balance quality and memory.");
        Assert.That(android.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), "Android should keep uncompressed texture to avoid border artifacts.");
    }

    [Test]
    public void TitleCanvas_PixelPerfect_IsEnabled()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            GameObject canvasObject = FindInTitleScene("Canvas - Title");
            Assert.That(canvasObject, Is.Not.Null, "Canvas - Title object was not found.");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null, "Canvas component was not found on Canvas - Title.");
            Assert.That(canvas.pixelPerfect, Is.True, "Canvas - Title should have Pixel Perfect enabled.");
        }
        finally
        {
            if (previous != null && previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    [Test]
    public void TmpSettings_DefaultFontAndFallback_AreAssigned()
    {
        Object settings = AssetDatabase.LoadMainAssetAtPath(TmpSettingsPath);
        Object notoFallback = AssetDatabase.LoadMainAssetAtPath(NotoFallbackFontPath);
        SerializedObject settingsSerialized = new SerializedObject(settings);
        SerializedProperty defaultFont = settingsSerialized.FindProperty("m_defaultFontAsset");
        SerializedProperty fallbackFonts = settingsSerialized.FindProperty("m_fallbackFontAssets");

        Assert.That(settings, Is.Not.Null, $"Missing TMP Settings asset: {TmpSettingsPath}");
        Assert.That(notoFallback, Is.Not.Null, $"Missing fallback font asset: {NotoFallbackFontPath}");
        Assert.That(defaultFont, Is.Not.Null, "TMP Settings default font property is missing.");
        Assert.That(defaultFont.objectReferenceValue, Is.Not.Null, "TMP Settings default font is not assigned.");
        Assert.That(fallbackFonts, Is.Not.Null, "TMP Settings fallback list property is missing.");

        bool hasNotoFallback = false;
        for (int i = 0; i < fallbackFonts.arraySize; i++)
        {
            SerializedProperty entry = fallbackFonts.GetArrayElementAtIndex(i);
            if (entry.objectReferenceValue == notoFallback)
            {
                hasNotoFallback = true;
                break;
            }
        }

        Assert.That(hasNotoFallback, Is.True, "TMP Settings fallback list must include NotoSansJP_Rebuilt SDF.");
    }

    [Test]
    public void TitleRoundedFont_HasJapaneseFallback()
    {
        Object titleRounded = AssetDatabase.LoadMainAssetAtPath(TitleRoundedFontPath);
        Object notoFallback = AssetDatabase.LoadMainAssetAtPath(NotoFallbackFontPath);
        SerializedObject titleSerialized = new SerializedObject(titleRounded);
        SerializedProperty fallbackFonts = titleSerialized.FindProperty("m_FallbackFontAssetTable");

        Assert.That(titleRounded, Is.Not.Null, $"Missing title rounded font asset: {TitleRoundedFontPath}");
        Assert.That(notoFallback, Is.Not.Null, $"Missing fallback font asset: {NotoFallbackFontPath}");
        Assert.That(fallbackFonts, Is.Not.Null, "Title rounded font fallback list property is null.");

        bool hasNotoFallback = false;
        for (int i = 0; i < fallbackFonts.arraySize; i++)
        {
            SerializedProperty entry = fallbackFonts.GetArrayElementAtIndex(i);
            if (entry.objectReferenceValue == notoFallback)
            {
                hasNotoFallback = true;
                break;
            }
        }

        Assert.That(hasNotoFallback, Is.True, "Title rounded font fallback must include NotoSansJP_Rebuilt SDF.");
    }

    [Test]
    public void TitleRoundedFont_IsDetectedAsValidAtlas()
    {
        TMP_FontAsset titleRounded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleRoundedFontPath);
        Assert.That(titleRounded, Is.Not.Null, $"Missing title rounded font asset: {TitleRoundedFontPath}");

        bool isInvalid = InvokeIsFontAtlasLikelyInvalid(titleRounded);
        Assert.That(isInvalid, Is.False, "Title rounded font atlas should be valid after rebuild.");
    }

    [Test]
    public void TitleScene_Stabilizer_LeavesHealthyFontsUntouched()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

            TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleRoundedFontPath);
            Assert.That(fallbackFont, Is.Not.Null, $"Missing title rounded font asset: {TitleRoundedFontPath}");

            TMP_Text[] texts = Resources
                .FindObjectsOfTypeAll<TMP_Text>()
                .Where(t =>
                    t != null &&
                    t.gameObject.scene.path == TitleScenePath &&
                    t.gameObject.activeInHierarchy)
                .ToArray();

            Assert.That(texts.Length, Is.GreaterThan(0), "No active TMP text was found in Title scene.");

            int replaced = InvokeStabilizeTexts(texts, fallbackFont);
            Assert.That(replaced, Is.EqualTo(0), "Healthy title fonts should not be rebound.");

            bool hasInvalidAfterStabilize = texts
                .Where(t => t != null && t.font != null)
                .Any(t => InvokeIsFontAtlasLikelyInvalid(t.font));

            Assert.That(hasInvalidAfterStabilize, Is.False, "Active TMP text still references an invalid atlas after stabilization.");
        }
        finally
        {
            if (previous != null && previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    private static string ReadStringField(Object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        return field.GetValue(target) as string;
    }

    private static int InvokeStabilizeTexts(TMP_Text[] texts, TMP_FontAsset fallbackFont)
    {
        MethodInfo method = GetStabilizerMethod(
            "StabilizeTexts",
            typeof(IReadOnlyList<TMP_Text>),
            typeof(TMP_FontAsset));

        object result = method.Invoke(null, new object[] { texts, fallbackFont });
        return result is int replaced ? replaced : 0;
    }

    private static bool InvokeIsFontAtlasLikelyInvalid(TMP_FontAsset fontAsset)
    {
        MethodInfo method = GetStabilizerMethod("IsFontAtlasLikelyInvalid", typeof(TMP_FontAsset));
        object result = method.Invoke(null, new object[] { fontAsset });
        return result is bool invalid && invalid;
    }

    private static MethodInfo GetStabilizerMethod(string methodName, params System.Type[] parameterTypes)
    {
        System.Type stabilizerType = System.Type.GetType("TitleSceneTmpStabilizer, Assembly-CSharp");
        Assert.That(stabilizerType, Is.Not.Null, "Type not found: TitleSceneTmpStabilizer, Assembly-CSharp");

        MethodInfo method = stabilizerType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            parameterTypes,
            null);

        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        return method;
    }

    private static MonoBehaviour FindControllerInTitleScene()
    {
        return Resources
            .FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(mb =>
                mb != null &&
                mb.gameObject.scene.path == TitleScenePath &&
                mb.GetType().Name == ControllerTypeName);
    }

    private static GameObject FindInTitleScene(string objectName)
    {
        return Resources
            .FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go =>
                go != null &&
                go.scene.path == TitleScenePath &&
                go.name == objectName);
    }
}
