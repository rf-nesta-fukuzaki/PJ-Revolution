using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// サウンド管理シングルトン。
/// AudioClip が未設定の場合はプロシージャル波形を生成して再生する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.3f;

    [Header("SE Clips (省略時はプロシージャル生成)")]
    [SerializeField] private AudioClip seRopeFire;
    [SerializeField] private AudioClip seRopeAttach;
    [SerializeField] private AudioClip seRopeSwing;
    [SerializeField] private AudioClip seFootstep;
    [SerializeField] private AudioClip seJump;
    [SerializeField] private AudioClip seLand;
    [SerializeField] private AudioClip seCheckpoint;
    [SerializeField] private AudioClip seSummit;

    private AudioSource _bgmSource;
    private AudioSource _seSource;
    private Dictionary<string, AudioClip> _seMap;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // DontDestroyOnLoad はルート GameObject にのみ適用可能。
        if (transform.parent != null)
            transform.SetParent(null, true);

        DontDestroyOnLoad(gameObject);

        SetupSources();
        BuildSeMap();
        PlayBGM(bgmClip, bgmVolume);
    }

    private void SetupSources()
    {
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.volume = bgmVolume;
        _bgmSource.playOnAwake = false;

        _seSource = gameObject.AddComponent<AudioSource>();
        _seSource.playOnAwake = false;
    }

    private void BuildSeMap()
    {
        _seMap = new Dictionary<string, AudioClip>
        {
            ["rope_fire"]   = seRopeFire   ?? GenerateTone(880f, 0.1f, WaveType.Square),
            ["rope_attach"] = seRopeAttach ?? GenerateTone(660f, 0.12f, WaveType.Sawtooth),
            ["rope_swing"]  = seRopeSwing  ?? GenerateNoise(0.08f),
            ["footstep"]    = seFootstep   ?? GenerateNoise(0.05f),
            ["jump"]        = seJump       ?? GenerateTone(440f, 0.08f, WaveType.Sine),
            ["land"]        = seLand       ?? GenerateNoise(0.1f),
            ["checkpoint"]  = seCheckpoint ?? GenerateArpeggio(),
            ["summit"]      = seSummit     ?? GenerateFanfare(),
        };
    }

    // ─── 公開 API ───

    public void PlayBGM(AudioClip clip, float volume = 0.3f)
    {
        if (_bgmSource == null) return;
        _bgmSource.volume = volume;
        if (clip != null)
        {
            _bgmSource.clip = clip;
            _bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        _bgmSource?.Stop();
    }

    public void PlaySE(string name)
    {
        if (_seSource == null) return;
        if (_seMap.TryGetValue(name, out AudioClip clip) && clip != null)
            _seSource.PlayOneShot(clip);
    }

    // ─── プロシージャル波形生成 ───

    private enum WaveType { Sine, Square, Sawtooth }

    private AudioClip GenerateTone(float freq, float duration, WaveType type)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float phase = t * freq * 2f * Mathf.PI;
            float val = 0f;
            switch (type)
            {
                case WaveType.Sine:      val = Mathf.Sin(phase); break;
                case WaveType.Square:    val = Mathf.Sin(phase) > 0f ? 1f : -1f; break;
                case WaveType.Sawtooth:  val = 2f * (t * freq - Mathf.Floor(t * freq + 0.5f)); break;
            }
            // エンベロープ（フェードアウト）
            float env = Mathf.Clamp01(1f - (float)i / samples * 2f);
            data[i] = val * env * 0.3f;
        }

        var clip = AudioClip.Create("proc_tone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateNoise(float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float env = Mathf.Clamp01(1f - (float)i / samples);
            data[i] = (Random.value * 2f - 1f) * env * 0.25f;
        }

        var clip = AudioClip.Create("proc_noise", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateArpeggio()
    {
        int sampleRate = 44100;
        float noteDuration = 0.08f;
        float[] notes = { 523.25f, 659.25f, 783.99f }; // C5, E5, G5
        int samplesPerNote = Mathf.CeilToInt(sampleRate * noteDuration);
        int totalSamples = samplesPerNote * notes.Length;
        float[] data = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            float freq = notes[n];
            for (int i = 0; i < samplesPerNote; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - (float)i / samplesPerNote * 1.5f);
                data[n * samplesPerNote + i] = Mathf.Sin(t * freq * 2f * Mathf.PI) * env * 0.3f;
            }
        }

        var clip = AudioClip.Create("proc_arpeggio", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateFanfare()
    {
        int sampleRate = 44100;
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5, E5, G5, C6
        float noteDur = 0.15f;
        int samplesPerNote = Mathf.CeilToInt(sampleRate * noteDur);
        int total = samplesPerNote * freqs.Length;
        float[] data = new float[total];

        for (int n = 0; n < freqs.Length; n++)
        {
            float freq = freqs[n];
            for (int i = 0; i < samplesPerNote; i++)
            {
                float t = (float)i / sampleRate;
                float env = i < samplesPerNote * 0.1f
                    ? (float)i / (samplesPerNote * 0.1f)
                    : Mathf.Clamp01(1f - ((float)i - samplesPerNote * 0.1f) / (samplesPerNote * 0.9f));
                data[n * samplesPerNote + i] = Mathf.Sin(t * freq * 2f * Mathf.PI) * env * 0.4f;
            }
        }

        var clip = AudioClip.Create("proc_fanfare", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
