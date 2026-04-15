// VivoxVoiceService.cs
// ─────────────────────────────────────────────────────────────────────────────
// ProximityVoiceChat 用リアル音声バックエンド。
// SDK が存在するときのみアクティブになる条件付きコンパイル構造。
//
// ◆ Vivox (Unity Gaming Services) を使う場合:
//   1. Package Manager → com.unity.services.vivox を追加
//   2. Project Settings → Player → Scripting Define Symbols に UNITY_VIVOX を追加
//   3. UGS ダッシュボードで Vivox プロジェクトを作成し AppID / SecretKey を設定
//
// ◆ Photon Voice 2 を使う場合:
//   1. Photon Voice 2 パッケージをインポート（Asset Store or SDKサイト）
//   2. Scripting Define Symbols に PHOTON_VOICE_DEFINED を追加
//   3. Photon Server Settings に AppID を設定
//
// ◆ どちらも未インストールの場合:
//   ProximityVoiceChat は AudioSource シミュレーションにフォールバック。
//   本ファイルは空の stub として動作する（コンパイルエラーなし）。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;

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
using Unity.Services.Vivox;
using Unity.Services.Core;

public class VivoxBackend : IVoiceBackend
{
    private IVivoxService _vivox;
    private string        _channelName;
    private bool          _isConnected;

    public bool IsConnected => _isConnected;

    public VivoxBackend()
    {
        _vivox = VivoxService.Instance;
    }

    public async void JoinChannel(string channelName, string playerId)
    {
        try
        {
            _channelName = channelName;

            // Vivox サインイン（オプション: 匿名ログイン）
            var loginOpts = new LoginOptions { DisplayName = playerId };
            await _vivox.LoginAsync(loginOpts);

            // 近接チャンネル（3D Positional）に参加
            var channelOpts = new Channel3DProperties(
                audibleDistance:   32f,
                conversationalDistance: 1f,
                audioFadeIntensityByDistance: 1f,
                audioFadeModel: AudioFadeModel.InverseByDistance);

            await _vivox.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, channelOpts);
            _isConnected = true;
            Debug.Log($"[Vivox] チャンネル参加: {channelName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Vivox] JoinChannel 失敗: {e.Message}");
        }
    }

    public async void LeaveChannel()
    {
        if (!_isConnected) return;
        try
        {
            await _vivox.LeaveChannelAsync(_channelName);
            await _vivox.LogoutAsync();
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
            int vivoxVol = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100);
            _vivox.SetInputDeviceVolume(vivoxVol);
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
            int vivoxVol = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100);
            _vivox.SetOutputDeviceVolume(vivoxVol);
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

        // ProximityVoiceChat が持つ Transform を使って位置を送信する。
        // Camera.main が存在する場合にリスナーとして使用。
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 listenerPos     = cam.transform.position;
        Vector3 listenerForward = cam.transform.forward;
        Vector3 listenerUp      = cam.transform.up;

        // スピーカー位置は距離・方向から概算（実際は各プレイヤー側から送信するのが理想）
        Vector3 speakerPos = listenerPos + cam.transform.forward * currentDistance;

        try
        {
            _vivox.Set3DPosition(speakerPos, listenerPos, listenerForward, listenerUp);
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
using Photon.Voice.Unity;
using Photon.Voice.PUN;

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
        // Photon Voice はルームへの参加で自動的に音声チャンネルに入る。
        // ルーム参加は PhotonNetwork.JoinOrCreateRoom で行う（ゲームロジック側）。
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

    public void SetReceiveVolume(float volume)
    {
        // Photon Voice は各 AudioSource で受信する。
        // AudioSource の volume は ProximityVoiceChat 側で制御済み。
    }

    public void UpdateProximity(float maxDistance, float currentDistance)
    {
        // Photon Voice では距離減衰を AudioSource の spatialBlend + rolloff で実装。
        // ProximityVoiceChat の ConfigureAudioSource() が担当。
    }

    public void Dispose() { }
}
#endif  // PHOTON_VOICE_DEFINED

// ─────────────────────────────────────────────────────────────────────────────
// ファクトリ — ProximityVoiceChat.CreateBackend() から呼ぶ
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 利用可能な SDK に応じて適切な IVoiceBackend を返すファクトリ。
/// SDK が未インストールの場合は null を返し（AudioSource フォールバック）。
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
        Debug.Log("[VoiceBackend] SDK 未検出 → AudioSource シミュレーションで動作");
        return null;
#endif
    }
}
