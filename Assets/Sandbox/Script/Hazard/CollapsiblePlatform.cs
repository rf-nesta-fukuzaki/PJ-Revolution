using System.Collections;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §7.1 L5 — 崩れ足場ハザード。
/// プレイヤーが乗ると一定時間後に崩れる。リセット可能。
/// </summary>
public class CollapsiblePlatform : MonoBehaviour
{
    [Header("崩れ設定")]
    [SerializeField] private float _collapseDelay    = 1.5f;   // 乗ってから崩れるまで（秒）
    [SerializeField] private float _respawnDelay     = 8f;     // 再出現までの時間
    [SerializeField] private bool  _canRespawn       = true;

    [Header("ぐらつき演出")]
    [SerializeField] private float _wobbleAmplitude  = 0.05f;
    [SerializeField] private float _wobbleFrequency  = 15f;

    private Vector3    _originalPos;
    private Quaternion _originalRot;
    private bool       _isCollapsing;
    private bool       _isCollapsed;
    private int        _playerCount;
    private Coroutine  _collapseCoroutine;
    private Renderer[] _renderers;
    private Collider[] _colliders;

    private void Awake()
    {
        _originalPos = transform.position;
        _originalRot = transform.rotation;
        _renderers   = GetComponentsInChildren<Renderer>();
        _colliders   = GetComponentsInChildren<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollapsed || !other.CompareTag("Player")) return;

        _playerCount++;
        if (!_isCollapsing && _collapseCoroutine == null)
            _collapseCoroutine = StartCoroutine(CollapseRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerCount = Mathf.Max(0, _playerCount - 1);
    }

    private IEnumerator CollapseRoutine()
    {
        _isCollapsing = true;
        float elapsed = 0f;

        // GDD §15.2 — floor_crumble_warn（ぐらつき開始で予兆音）
        GameServices.Audio?.PlaySE(SoundId.FloorCrumbleWarn, transform.position);

        // ぐらつき演出
        while (elapsed < _collapseDelay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _collapseDelay;
            float wobbleX = Mathf.Sin(Time.time * _wobbleFrequency) * _wobbleAmplitude * t;
            float wobbleZ = Mathf.Cos(Time.time * _wobbleFrequency * 0.8f) * _wobbleAmplitude * t;
            transform.position = _originalPos + new Vector3(wobbleX, 0f, wobbleZ);
            yield return null;
        }

        // 崩落
        Collapse();

        if (_canRespawn)
        {
            yield return new WaitForSeconds(_respawnDelay);
            Respawn();
        }

        _collapseCoroutine = null;
    }

    private void Collapse()
    {
        _isCollapsed  = true;
        _isCollapsing = false;
        Debug.Log($"[CollapsiblePlatform] {name} が崩れた！");

        // GDD §15.2 — floor_crumble（実際の崩落）
        GameServices.Audio?.PlaySE(SoundId.FloorCrumble, transform.position);

        // 崩落の砂煙 poof（足場サイズに比例）。renderer/collider を無効化する前に bounds を取得する。
        Vector3 fxCenter = transform.position;
        float fxScale = 1.2f;
        if (_colliders != null && _colliders.Length > 0 && _colliders[0] != null)
        {
            var b = _colliders[0].bounds;
            fxCenter = b.center;
            fxScale = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.4f, 0.9f, 3f);
        }
        Sandbox.World.Environment.StylizedImpactFx.Spawn(fxCenter, new Color(0.55f, 0.50f, 0.42f), fxScale, 26);

        foreach (var r in _renderers) r.enabled  = false;
        foreach (var c in _colliders) c.enabled  = false;

        transform.position = _originalPos;
    }

    private void Respawn()
    {
        _isCollapsed = false;
        _playerCount = 0;

        foreach (var r in _renderers) r.enabled = true;
        foreach (var c in _colliders) c.enabled = true;
    }
}
