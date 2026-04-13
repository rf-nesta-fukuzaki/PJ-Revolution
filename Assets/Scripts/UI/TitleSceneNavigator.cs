using System;
using System.IO;
using UnityEngine.SceneManagement;

public interface ITitleSceneNavigator
{
    bool Exists(string sceneName);
    void Load(string sceneName);
}

public sealed class UnityTitleSceneNavigator : ITitleSceneNavigator
{
    public bool Exists(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string candidate = Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(candidate, sceneName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
