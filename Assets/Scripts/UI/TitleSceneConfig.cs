using UnityEngine;

[CreateAssetMenu(menuName = "Title/Title Scene Config", fileName = "TitleSceneConfig")]
public sealed class TitleSceneConfig : ScriptableObject
{
    public const string DefaultPreloadCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !?.,:;+-*/()[]{}<>_@#$%^&=|'\"`~©";

    [Header("Scene")]
    public string startSceneName = "TestScene";

    [Header("Animation")]
    public bool useUnscaledTime = true;
    [Min(0.05f)] public float introTitleDuration = 0.5f;
    [Min(0.05f)] public float introMenuDuration = 0.38f;
    [Min(0.05f)] public float introButtonDuration = 0.27f;
    [Min(0f)] public float introButtonStagger = 0.075f;
    [Min(0f)] public float introTitleOffsetY = 48f;
    [Min(0f)] public float introMenuOffsetY = 14f;
    [Min(0f)] public float introButtonOffsetY = 16f;
    [Min(0f)] public float hoverScale = 1.03f;
    [Min(0f)] public float hoverOffsetX = 4f;
    [Min(0f)] public float hoverOffsetY = 1f;
    [Range(0.8f, 1f)] public float pressedScale = 0.965f;
    [Min(0.05f)] public float hoverTweenDuration = 0.12f;
    [Min(0f)] public float backgroundFloatStrength = 10f;
    [Min(0.2f)] public float backgroundFloatDuration = 11f;

    [Header("Font Stability")]
    [TextArea(2, 4)] public string preloadCharacters = DefaultPreloadCharacters;
}
