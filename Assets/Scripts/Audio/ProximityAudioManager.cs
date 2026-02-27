using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーン全体のプレイヤー近接音声を一括管理する MonoBehaviour。
/// ローカルプレイヤーと各リモートプレイヤーの距離を毎フレーム計算し、
/// リモートプレイヤーの ProximityAudioSource に音量・エコー強度を指示する。
///
/// [距離減衰ルール]
///   - 0 〜 fullVolumeRadius   : 音量 1.0 / エコーなし
///   - fullVolumeRadius 〜 fadeOutRadius : 線形または AnimationCurve で減衰
///   - fadeOutRadius 以上       : 音量 0.0 / エコー最大
///
///
/// [セットアップ]
///   シーン内の適当な空 GameObject にアタッチする。
///   Inspector でパラメータを調整し、AudioVolumeCurve は省略可（線形フォールバック）。
/// </summary>
public class ProximityAudioManager : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("距離パラメータ")]
    [Tooltip("この距離以内ではフル音量（m）")]
    [SerializeField] private float _fullVolumeRadius = 10f;

    [Tooltip("この距離以上で完全無音になる（m）")]
    [SerializeField] private float _fadeOutRadius = 40f;

    [Header("音量カーブ")]
    [Tooltip("fullVolumeRadius〜fadeOutRadius 間の減衰カーブ（X=0:近端, X=1:遠端, Y=音量）。" +
             "未設定時は線形減衰")]
    [SerializeField] private AnimationCurve _volumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("エコー設定")]
    [Tooltip("エコーが始まる距離（m）。通常 fullVolumeRadius と同じか少し大きい値にする）")]
    [SerializeField] private float _echoStartRadius = 10f;

    [Tooltip("エコーが最大になる距離（m）。通常 fadeOutRadius と合わせる")]
    [SerializeField] private float _echoFullRadius = 40f;

    [Header("更新間隔")]
    [Tooltip("距離計算を行う間隔（秒）。0 で毎フレーム計算。パフォーマンス重視なら 0.1 程度）")]
    [SerializeField] private float _updateInterval = 0f;

    [Tooltip("ローカルプレイヤー未検出時に ProximityAudioSource を再スキャンする間隔（秒）。" +
             "プレイヤースポーン前の FindObjectsByType 連打を防ぐ。")]
    [SerializeField] private float _rescanInterval = 0.5f;

    // ─────────────── 内部状態 ───────────────

    /// <summary>シーン内の全 ProximityAudioSource キャッシュ。</summary>
    private readonly List<ProximityAudioSource> _sources = new();

    /// <summary>ローカルプレイヤーの ProximityAudioSource（音量計算の基点）。</summary>
    private ProximityAudioSource _localSource;

    private float _timeSinceLastUpdate;
    private float _rescanTimer;

    // ─────────────── Unity Lifecycle ───────────────

    private void Start()
    {
        RefreshSources();
    }

    private void Update()
    {
        _timeSinceLastUpdate += Time.deltaTime;
        if (_updateInterval > 0f && _timeSinceLastUpdate < _updateInterval) return;
        _timeSinceLastUpdate = 0f;

        // ローカルプレイヤーを未取得またはロスト済みなら _rescanInterval 間隔で再スキャン。
        // 毎フレーム FindObjectsByType を呼ぶとスポーン前の期間にログが大量発生するため抑制する。
        if (_localSource == null)
        {
            _rescanTimer += Time.deltaTime;
            if (_rescanTimer < _rescanInterval) return;
            _rescanTimer = 0f;
            RefreshSources();
        }

        if (_localSource == null) return;

        Vector3 localPos = _localSource.transform.position;

        foreach (var source in _sources)
        {
            if (source == null)   continue;
            if (source == _localSource) continue; // 自分自身はスキップ

            float dist = Vector3.Distance(localPos, source.transform.position);
            float vol  = CalcVolume(dist);
            float echo = CalcEchoBlend(dist);
            source.ApplyProximity(vol, echo);
        }
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// プレイヤースポーン後にシーン内の ProximityAudioSource を再スキャンする。
    /// </summary>
    public void RefreshSources()
    {
        _sources.Clear();
        _localSource = null;

        var all = FindObjectsByType<ProximityAudioSource>(FindObjectsSortMode.None);
        foreach (var s in all)
        {
            _sources.Add(s);
            if (s.IsLocalPlayer)
                _localSource = s;
        }

        Debug.Log($"[ProximityAudioManager] AudioSource スキャン完了: " +
                  $"合計={_sources.Count}, ローカル={(_localSource != null ? "あり" : "なし")}");
    }

    // ─────────────── 内部計算 ───────────────

    /// <summary>
    /// 距離から 0〜1 の音量係数を算出する。
    /// </summary>
    private float CalcVolume(float distance)
    {
        if (distance <= _fullVolumeRadius) return 1f;
        if (distance >= _fadeOutRadius)   return 0f;

        float t = Mathf.InverseLerp(_fullVolumeRadius, _fadeOutRadius, distance);

        // AnimationCurve が設定されていれば使用、なければ線形
        if (_volumeCurve != null && _volumeCurve.length >= 2)
            return Mathf.Clamp01(_volumeCurve.Evaluate(t));

        return 1f - t;
    }

    /// <summary>
    /// 距離から 0〜1 のエコー強度（reverbBlend）を算出する。
    /// </summary>
    private float CalcEchoBlend(float distance)
    {
        if (distance <= _echoStartRadius) return 0f;
        if (distance >= _echoFullRadius)  return 1f;
        return Mathf.InverseLerp(_echoStartRadius, _echoFullRadius, distance);
    }

    // ─────────────── Gizmos ───────────────

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        // fullVolumeRadius: 緑
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(center, _fullVolumeRadius);

        // fadeOutRadius: 赤
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(center, _fadeOutRadius);
    }
}
