using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// GDD §8.3 — NGO起動前に Unity Gaming Services を初期化し、匿名サインインを実行する。
/// シーン最初に実行されるシングルトン。初期化完了後に OnReady イベントを発火する。
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    public bool IsReady    { get; private set; }
    public bool HasError   { get; private set; }
    public string ErrorMessage { get; private set; }

    // 初期化完了通知
    public event System.Action OnReady;
    public event System.Action<string> OnInitError;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);   // ルートに移動してから DDOL を呼ぶ
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _ = InitializeAsync();
    }

    // ── 初期化 ──────────────────────────────────────────────────
    private async Task InitializeAsync()
    {
        try
        {
            // エディタ上での複数セッション衝突を防ぐ
#if UNITY_EDITOR
            var options = new InitializationOptions();
            options.SetProfile(UnityEngine.Random.Range(0, 10000).ToString());
            await UnityServices.InitializeAsync(options);
#else
            await UnityServices.InitializeAsync();
#endif

            await SignInAnonymouslyAsync();

            IsReady = true;
            Debug.Log($"[Bootstrap] UGS準備完了 — PlayerID: {AuthenticationService.Instance.PlayerId}");
            OnReady?.Invoke();
        }
        catch (System.Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
            Debug.LogError($"[Bootstrap] UGS初期化失敗: {ex.Message}");
            OnInitError?.Invoke(ex.Message);
        }
    }

    private async Task SignInAnonymouslyAsync()
    {
        if (AuthenticationService.Instance.IsSignedIn) return;

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log("[Bootstrap] 匿名サインイン完了");
    }

    // ── エラー時リトライ ─────────────────────────────────────────
    public void Retry()
    {
        HasError     = false;
        ErrorMessage = null;
        _ = InitializeAsync();
    }
}
