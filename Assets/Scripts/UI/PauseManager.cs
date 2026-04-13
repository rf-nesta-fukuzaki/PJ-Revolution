using UnityEngine;

/// <summary>
/// ESC キーでオプション画面をトグルするポーズ管理コンポーネント。
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] private GameObject optionsPanel;

    private void Update()
    {
        if (InputStateReader.EscapePressedThisFrame() && Cursor.lockState == CursorLockMode.Locked)
            TogglePause();
    }

    private void OnDisable()
    {
        if (IsPaused) SetPaused(false);
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        if (optionsPanel != null)
            optionsPanel.SetActive(paused);

        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void TogglePause() => SetPaused(!IsPaused);
}
