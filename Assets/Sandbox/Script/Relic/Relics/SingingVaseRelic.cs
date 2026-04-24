using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §6.2 — 遺物④「歌う壺」
/// 物理軸：音が出る（ボイチャ妨害）。
/// 揺れると古代の歌が鳴り響く。激しく揺れると大音量で落石を誘発。
/// 難易度：★★★  壊れやすさ：高
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SingingVaseRelic : RelicBase
{
    private static readonly List<SingingVaseRelic> s_registeredRelics = new();

    [Header("歌設定")]
    [SerializeField] private AudioClip[] _songClips;            // 古代の歌（複数）
    [SerializeField] private float       _singSeedShakeSpeed = 1.5f;   // この速さで歌い始める
    [SerializeField] private float       _loudSoundThreshold = 4f;     // この速さで大音量モード
    [SerializeField] private float       _rockfallTriggerVolume = 0.9f; // この音量で落石誘発

    [Header("落石誘発")]
    [SerializeField] private float _rockfallRadius = 15f;

    private AudioSource _audio;
    private float       _currentVolume;
    private bool        _isLoud;

    // ── プロシージャルオーディオ定数 ─────────────────────────
    private const int   SAMPLE_RATE   = 22050;   // 省メモリのため 22kHz
    private const float NOTE_DURATION = 1.2f;    // 1音の長さ（秒）

    // 外部からボイチャへの妨害量を取得するためのプロパティ
    public float VoiceChatInterference => _currentVolume;
    public static IReadOnlyList<SingingVaseRelic> RegisteredRelics => s_registeredRelics;

    private void OnEnable()
    {
        if (!s_registeredRelics.Contains(this))
            s_registeredRelics.Add(this);
    }

    private void OnDisable()
    {
        s_registeredRelics.Remove(this);
    }

    protected override void Awake()
    {
        _relicName        = "歌う壺";
        _baseValue        = 150;
        _maxHp            = 60f;
        _damageMultiplier = 4f;
        _impactThreshold  = 1f;

        base.Awake();

        _audio = GetComponent<AudioSource>();
        _audio.spatialBlend = 1f;   // 3D音響
        _audio.loop         = true;

        // AudioClip が Inspector でアサインされていない場合はプロシージャル生成
        if (_songClips == null || _songClips.Length == 0)
            _songClips = GenerateProceduralSongs();
    }

    private void Update()
    {
        if (_isDestroyed) return;

        float speed = _rb.linearVelocity.magnitude + _rb.angularVelocity.magnitude;
        UpdateSinging(speed);
    }

    private void UpdateSinging(float shakeSpeed)
    {
        float targetVolume = 0f;

        if (shakeSpeed > _singSeedShakeSpeed)
        {
            targetVolume = Mathf.InverseLerp(_singSeedShakeSpeed, _loudSoundThreshold * 2f, shakeSpeed);
        }

        _currentVolume = Mathf.Lerp(_currentVolume, targetVolume, Time.deltaTime * 3f);
        _audio.volume  = _currentVolume;

        // 歌い始め
        if (_currentVolume > 0.05f && !_audio.isPlaying && _songClips != null && _songClips.Length > 0)
        {
            _audio.clip = _songClips[UnityEngine.Random.Range(0, _songClips.Length)];
            _audio.Play();
            // GDD §15.2 — relic_pot_sing（歌い始めのエッジトリガー SE）
            PPAudioManager.Instance?.PlaySE(SoundId.RelicPotSing, transform.position);
        }
        else if (_currentVolume <= 0.05f && _audio.isPlaying)
        {
            _audio.Stop();
        }

        // 大音量モード
        bool wasLoud = _isLoud;
        _isLoud = _currentVolume > _rockfallTriggerVolume;

        if (_isLoud && !wasLoud)
        {
            Debug.Log("[SingingVase] 大音量！「うるさい黙れ壺！」");
            TriggerNearbyRockfalls();
        }
    }

    private void TriggerNearbyRockfalls()
    {
        // 半径内の RockfallTrigger を全て起動
        foreach (var t in RockfallTrigger.RegisteredTriggers)
        {
            if (Vector3.Distance(transform.position, t.transform.position) <= _rockfallRadius)
                t.Activate();
        }
    }

    protected override Color GizmoColor => new Color(0.79f, 0.38f, 0.24f);

    protected override void BuildVisual()
    {
        var terra = new Color(0.79f, 0.38f, 0.24f);
        var dark  = new Color(0.48f, 0.22f, 0.12f);

        // 胴体
        VizChild(PrimitiveType.Sphere, "body",
            new Vector3(0f, 0.1f, 0f), new Vector3(1.0f, 1.1f, 1.0f),
            terra, smoothness: 0.45f);
        // 首
        VizChild(PrimitiveType.Cylinder, "neck",
            new Vector3(0f, 0.72f, 0f), new Vector3(0.35f, 0.38f, 0.35f),
            terra, smoothness: 0.45f);
        // 口
        VizChild(PrimitiveType.Cylinder, "mouth",
            new Vector3(0f, 0.98f, 0f), new Vector3(0.55f, 0.12f, 0.55f),
            terra, smoothness: 0.45f);
        // 底
        VizChild(PrimitiveType.Cylinder, "foot",
            new Vector3(0f, -0.58f, 0f), new Vector3(0.45f, 0.18f, 0.45f),
            dark, smoothness: 0.3f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        _audio.Stop();
        Debug.Log("[SingingVase] 壺が壊れた。静かになった。");
    }

    // ── プロシージャルオーディオ生成 ─────────────────────────
    /// <summary>
    /// AudioClip が未設定のとき、サイン波合成で古代の歌を3パターン生成する。
    /// AudioClip.Create を使用するため外部アセット不要。
    /// </summary>
    private static AudioClip[] GenerateProceduralSongs()
    {
        Debug.Log("[SingingVase] プロシージャルオーディオを生成中…");
        return new[]
        {
            // 古代の低音ドローン（A2 + A3 倍音重ね）
            GenerateDrone(110f, 220f, 4.0f),
            // 五音音階メロディ（C ペンタトニック）
            GeneratePentatonicMelody(new float[] { 261.6f, 293.7f, 329.6f, 392.0f, 440.0f }, NOTE_DURATION),
            // 不気味な三度音程ループ（D + F）
            GenerateDrone(293.7f, 349.2f, 3.0f),
        };
    }

    /// <summary>2つの周波数をベースにしたドローン音（うなりを含む）を生成する。</summary>
    private static AudioClip GenerateDrone(float freqA, float freqB, float duration)
    {
        int sampleCount = Mathf.RoundToInt(SAMPLE_RATE * duration);
        var samples     = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t    = (float)i / SAMPLE_RATE;
            // フェードイン(前10%) + サステイン + フェードアウト(後10%)
            float env  = CalcEnvelope(i, sampleCount, 0.10f, 0.10f);
            float wave = Mathf.Sin(2f * Mathf.PI * freqA * t) * 0.45f
                       + Mathf.Sin(2f * Mathf.PI * freqB * t) * 0.30f;
            // 奇数倍音を加えて "古い陶器" 感を演出
            wave += Mathf.Sin(2f * Mathf.PI * freqA * 3f * t) * 0.12f;
            samples[i] = wave * env;
        }

        var clip = AudioClip.Create($"drone_{freqA:F0}_{freqB:F0}",
                                    sampleCount, 1, SAMPLE_RATE, stream: false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>指定の音符列でペンタトニックメロディを合成する。</summary>
    private static AudioClip GeneratePentatonicMelody(float[] notes, float noteDur)
    {
        int samplesPerNote = Mathf.RoundToInt(SAMPLE_RATE * noteDur);
        int totalSamples   = samplesPerNote * notes.Length;
        var samples        = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            int   start = n * samplesPerNote;
            float freq  = notes[n];

            for (int i = 0; i < samplesPerNote; i++)
            {
                float t   = (float)i / SAMPLE_RATE;
                float env = CalcEnvelope(i, samplesPerNote, 0.08f, 0.20f);
                samples[start + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f * env;
            }
        }

        var clip = AudioClip.Create("pentatonic_melody",
                                    totalSamples, 1, SAMPLE_RATE, stream: false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>フェードイン/アウトエンベロープ（0〜1）を計算する。</summary>
    private static float CalcEnvelope(int sampleIndex, int totalSamples,
                                      float fadeInRatio, float fadeOutRatio)
    {
        float t    = (float)sampleIndex / totalSamples;
        float fade = 1f;

        if (t < fadeInRatio)
            fade = t / fadeInRatio;
        else if (t > 1f - fadeOutRatio)
            fade = (1f - t) / fadeOutRatio;

        return Mathf.Clamp01(fade);
    }
}
