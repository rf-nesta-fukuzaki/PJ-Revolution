// VivoxVoiceService.cs
// ─────────────────────────────────────────────────────────────────────────────
// ProximityVoiceChat 用リアル音声バックエンド。
// SDK が存在するときのみアクティブになる条件付きコンパイル構造。
//
// ◆ Vivox (Unity Gaming Services) を使う場合:
//   1. Package Manager → com.unity.services.vivox を追加（導入済み: 16.10.0）
//   2. Project Settings → Player → Scripting Define Symbols に UNITY_VIVOX を追加
//   3. UGS ダッシュボードでプロジェクトをリンクし、Vivox を有効化
//      （Edit > Project Settings > Services でプロジェクト ID を紐付け）
//
// ◆ Photon Voice 2 を使う場合:
//   1. Photon Voice 2 パッケージをインポート
//   2. Scripting Define Symbols に PHOTON_VOICE_DEFINED を追加
//
// ◆ どちらも未インストールの場合:
//   ProximityVoiceChat は AudioSource シミュレーションにフォールバック。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;
#if UNITY_VIVOX
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
#endif
#if PHOTON_VOICE_DEFINED
using Photon.Voice.Unity;
using Photon.Voice.PUN;
#endif

/// <summary>
/// リアル音声バックエンドの共通インターフェース。
/// ProximityVoiceChat はこれを通じて SDK 固有の処理を呼び出す。
/// </summary>
public interface IVoiceBackend : IDisposable
{
    /// <summary>ローカルプレイヤーとしてチャンネルに参加する。</summary>
    void JoinChannel(string channelName, string playerId);

    /// <summary>チャンネルから退出する。</summary>
    void LeaveChannel();

    /// <summary>送信音量を 0〜1 で設定する。</summary>
    void SetTransmitVolume(float volume);

    /// <summary>受信音量を 0〜1 で設定する（全員分一括）。</summary>
    void SetReceiveVolume(float volume);

    /// <summary>距離減衰モデルをバックエンドに通知する（対応 SDK のみ有効）。</summary>
    void UpdateProximity(float maxDistance, float currentDistance);

    bool IsConnected { get; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Vivox バックエンド実装
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_VIVOX
public class VivoxBackend : IVoiceBackend
{
    private string _channelName;
    private bool   _isConnected;

    public bool IsConnected => _isConnected;

    public VivoxBackend() { }

    public async void JoinChannel(string channelName, string playerId)
    {
        try
        {
            _channelName = channelName;

            // UGS Core 初期化 & 匿名認証（未済の場合のみ）
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // Vivox 初期化 & ログイン
            await VivoxService.Instance.InitializeAsync();
            var loginOpts = new LoginOptions { DisplayName = playerId };
            await VivoxService.Instance.LoginAsync(loginOpts);

            // 3D 近接チャンネル（audibleDistance/conversationalDistance は int）
            var channelOpts = new Channel3DProperties(
                audibleDistance: 32,
                conversationalDistance: 1,
                audioFadeIntensityByDistanceaudio: 1f,
                audioFadeModel: AudioFadeModel.InverseByDistance);

            await VivoxService.Instance.JoinPositionalChannelAsync(
                channelName, ChatCapability.AudioOnly, channelOpts);

            _isConnected = true;
            Debug.Log($"[Vivox] チャンネル参加: {channelName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Vivox] JoinChannel 失敗: {e.Message}（UGS プロジェクトのリンク/Vivox 有効化を確認してください）");
        }
    }

    public async void LeaveChannel()
    {
        if (!_isConnected) return;
        try
        {
            await VivoxService.Instance.LeaveChannelAsync(_channelName);
            await VivoxService.Instance.LogoutAsync();
            _isConnected = false;
            Debug.Log("[Vivox] チャンネル退出");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Vivox] LeaveChannel 失敗: {e.Message}");
        }
    }

    public void SetTransmitVolume(float volume)
    {
        try
        {
            // Vivox の入力デバイス音量は -50〜50（0=既定）
            int vivoxVol = Mathf.RoundToInt(Mathf.Lerp(-50f, 50f, Mathf.Clamp01(volume)));
            VivoxService.Instance.SetInputDeviceVolume(vivoxVol);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Vivox] SetTransmitVolume 失敗: {e.Message}");
        }
    }

    public void SetReceiveVolume(float volume)
    {
        try
        {
            int vivoxVol = Mathf.RoundToInt(Mathf.Lerp(-50f, 50f, Mathf.Clamp01(volume)));
            VivoxService.Instance.SetOutputDeviceVolume(vivoxVol);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Vivox] SetReceiveVolume 失敗: {e.Message}");
        }
    }

    /// <summary>
    /// 3D 近接モデル: スピーカー位置をリスナー（ローカルカメラ）基準で更新する。
    /// Vivox Positional Channel が自動的に距離減衰を計算する。
    /// </summary>
    public void UpdateProximity(float maxDistance, float currentDistance)
    {
        if (!_isConnected) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 listenerPos     = cam.transform.position;
        Vector3 listenerForward = cam.transform.forward;
        Vector3 listenerUp      = cam.transform.up;
        Vector3 speakerPos      = listenerPos + cam.transform.forward * currentDistance;

        try
        {
            VivoxService.Instance.Set3DPosition(speakerPos, listenerPos, listenerForward, listenerUp, _channelName);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Vivox] UpdateProximity 失敗: {e.Message}");
        }
    }

    public void Dispose() { }
}
#endif  // UNITY_VIVOX

// ─────────────────────────────────────────────────────────────────────────────
// Photon Voice 2 バックエンド実装
// ─────────────────────────────────────────────────────────────────────────────
#if PHOTON_VOICE_DEFINED
public class PhotonVoiceBackend : IVoiceBackend
{
    private VoiceConnection _voiceConn;
    private Recorder        _recorder;

    public bool IsConnected => _voiceConn != null && _voiceConn.Client.IsConnected;

    public PhotonVoiceBackend(VoiceConnection voiceConn, Recorder recorder)
    {
        _voiceConn = voiceConn;
        _recorder  = recorder;
    }

    public void JoinChannel(string channelName, string playerId)
    {
        if (_recorder != null) _recorder.TransmitEnabled = true;
        Debug.Log($"[PhotonVoice] 送信開始: {playerId}");
    }

    public void LeaveChannel()
    {
        if (_recorder != null) _recorder.TransmitEnabled = false;
        Debug.Log("[PhotonVoice] 送信停止");
    }

    public void SetTransmitVolume(float volume)
    {
        if (_recorder != null) _recorder.TransmitEnabled = volume > 0.01f;
    }

    public void SetReceiveVolume(float volume) { }

    public void UpdateProximity(float maxDistance, float currentDistance) { }

    public void Dispose() { }
}
#endif  // PHOTON_VOICE_DEFINED

// ─────────────────────────────────────────────────────────────────────────────
// ファクトリ — ProximityVoiceChat.OnNetworkSpawn() から呼ぶ
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 利用可能な SDK に応じて適切な IVoiceBackend を返すファクトリ。
/// SDK が未インストールの場合は null を返す（AudioSource フォールバック）。
/// </summary>
public static class VoiceBackendFactory
{
    /// <param name="go">VoiceConnection / Recorder コンポーネントを探す GameObject（Photon 用）</param>
    public static IVoiceBackend Create(GameObject go)
    {
#if UNITY_VIVOX
        Debug.Log("[VoiceBackend] Vivox バックエンドを使用");
        return new VivoxBackend();
#elif PHOTON_VOICE_DEFINED
        var conn     = go.GetComponentInParent<VoiceConnection>();
        var recorder = go.GetComponentInParent<Recorder>();
        if (conn == null || recorder == null)
        {
            Debug.LogWarning("[VoiceBackend] PhotonVoice: VoiceConnection または Recorder が見つかりません。AudioSource フォールバックを使用します");
            return null;
        }
        Debug.Log("[VoiceBackend] Photon Voice バックエンドを使用");
        return new PhotonVoiceBackend(conn, recorder);
#else
        Debug.Log("[VoiceBackend] SDK 未検出 → SimulatedVoiceBackend で動作");
        return new SimulatedVoiceBackend(go);
#endif
    }
}
