using NUnit.Framework;
using PeakPlunder.Localization;

public sealed class LocalizedTextFallbackTests
{
    private string _previousFallbackLanguageCode;

    [SetUp]
    public void SetUp()
    {
        _previousFallbackLanguageCode = LocalizedText.CurrentFallbackLanguageCode;
    }

    [TearDown]
    public void TearDown()
    {
        LocalizedText.SetFallbackLanguage(_previousFallbackLanguageCode);
    }

    [Test]
    public void Get_UsesEnglishFallback_WhenLanguageIsEnglish()
    {
        LocalizedText.SetFallbackLanguage("en");
        string value = LocalizedText.Get(LocalizationKeys.UIMainMenuPlay, "__unknown_table__");

        Assert.That(value, Is.EqualTo("Play"));
    }

    [Test]
    public void Get_UsesJapaneseFallback_WhenLanguageIsJapanese()
    {
        LocalizedText.SetFallbackLanguage("ja");
        string value = LocalizedText.Get(LocalizationKeys.UIMainMenuPlay, "__unknown_table__");

        Assert.That(value, Is.EqualTo("プレイ"));
    }

    [Test]
    public void SetFallbackLanguage_NormalizesUnsupportedCode_ToJapanese()
    {
        LocalizedText.SetFallbackLanguage("fr");
        Assert.That(LocalizedText.CurrentFallbackLanguageCode, Is.EqualTo("ja"));
    }
}
