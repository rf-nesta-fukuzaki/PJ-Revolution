using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Peak Idiots のゲーム状態管理シングルトン。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Playing;

    public static event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        ChangeState(GameState.Playing);
    }

    public void NotifySummitReached()
    {
        if (CurrentState != GameState.Playing) return;
        ChangeState(GameState.Clear);
    }

    public void RetryGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }
}

public enum GameState
{
    Playing,
    Clear,
    Paused,
}
