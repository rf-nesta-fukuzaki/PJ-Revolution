using UnityEngine;

/// <summary>
/// ゲーム開始から山頂到達までの時間を計測する。
/// mm:ss.ff 形式で表示し、ベストタイムを PlayerPrefs に保存する。
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    public static TimerDisplay Instance { get; private set; }

    private float _elapsed;
    private bool _running = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_running)
            _elapsed += Time.deltaTime;
    }

    public void Stop() => _running = false;
    public void Restart() { _elapsed = 0f; _running = true; }

    public float GetElapsedTime() => _elapsed;

    public string GetFormattedTime()
    {
        int min = Mathf.FloorToInt(_elapsed / 60f);
        float sec = _elapsed % 60f;
        return $"{min:00}:{sec:00.00}";
    }
}
