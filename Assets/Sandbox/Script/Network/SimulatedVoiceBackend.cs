using System;
using UnityEngine;

/// <summary>
/// Vivox / Photon Voice 未導入時のプロキシミティ VC シミュレーション。
/// V キー（ホールド）で低音量のハム音を再生し、距離減衰は ProximityVoiceChat が担当する。
/// </summary>
public sealed class SimulatedVoiceBackend : IVoiceBackend
{
    private readonly AudioSource _source;
    private readonly AudioClip   _humClip;
    private float _transmitVolume = 1f;
    private bool  _pushToTalkActive;

    public bool IsConnected { get; private set; }

    public SimulatedVoiceBackend(GameObject host)
    {
        _source = host.GetComponent<AudioSource>();
        if (_source == null)
            _source = host.AddComponent<AudioSource>();

        _humClip = CreateHumClip();
        _source.clip          = _humClip;
        _source.loop          = true;
        _source.playOnAwake   = false;
        _source.spatialBlend  = 1f;
        _source.volume        = 0f;
        _source.minDistance   = 1f;
        _source.maxDistance   = 15f;
        _source.rolloffMode   = AudioRolloffMode.Linear;
    }

    public void JoinChannel(string channelName, string playerId)
    {
        IsConnected = true;
        Debug.Log($"[SimulatedVoice] チャンネル参加（シミュレーション）: {channelName}");
    }

    public void LeaveChannel()
    {
        IsConnected = false;
        _pushToTalkActive = false;
        if (_source != null && _source.isPlaying)
            _source.Stop();
    }

    public void SetTransmitVolume(float volume)
    {
        _transmitVolume = Mathf.Clamp01(volume);
        ApplyVolume();
    }

    public void SetReceiveVolume(float volume) { /* 受信は各 AudioSource の maxDistance で表現 */ }

    public void UpdateProximity(float maxDistance, float currentDistance) { }

    /// <summary>ProximityVoiceChat から V キー状態を毎フレーム通知する。</summary>
    public void SetPushToTalkActive(bool active)
    {
        _pushToTalkActive = active;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (_source == null) return;

        float target = _pushToTalkActive ? 0.18f * _transmitVolume : 0f;
        _source.volume = target;

        if (target > 0.01f && !_source.isPlaying)
            _source.Play();
        else if (target <= 0.01f && _source.isPlaying)
            _source.Stop();
    }

    public void Dispose()
    {
        LeaveChannel();
    }

    private static AudioClip CreateHumClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.5f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        var samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            samples[i] = (Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.25f
                        + (UnityEngine.Random.value * 2f - 1f) * 0.08f);
        }

        var clip = AudioClip.Create("sim_voice_hum", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
