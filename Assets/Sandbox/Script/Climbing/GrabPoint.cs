using UnityEngine;

/// <summary>
/// GDD §3.1 — 登攀用グラブポイント。
/// 壁・岩に配置し、プレイヤーが近づくとハイライト表示する。
/// </summary>
public class GrabPoint : MonoBehaviour
{
    [Header("グラブポイント設定")]
    [SerializeField] private float  _grabRadius    = 0.6f;
    [SerializeField] private bool   _requireIceAxe = false;   // 氷壁グリップが必要か
    [SerializeField] private float  _staminaDrain  = 5f;      // 毎秒スタミナ消費量

    [Header("ハイライト")]
    [SerializeField] private Color  _highlightColor  = Color.green;
    [SerializeField] private Color  _defaultColor    = Color.white;
    [SerializeField] private Renderer _markerRenderer;

    private bool _isHighlighted;
    private bool _isOccupied;
    private MaterialPropertyBlock _propertyBlock;

    public float GrabRadius     => _grabRadius;
    public bool  RequireIceAxe  => _requireIceAxe;
    public float StaminaDrain   => _staminaDrain;
    public bool  IsOccupied     => _isOccupied;

    private void Start()
    {
        _propertyBlock ??= new MaterialPropertyBlock();
        if (_markerRenderer == null)
            _markerRenderer = GetComponentInChildren<Renderer>();

        SetHighlight(false);
    }

    // ── ハイライト ────────────────────────────────────────────
    public void SetHighlight(bool on)
    {
        if (_isHighlighted == on) return;
        _isHighlighted = on;

        if (_markerRenderer == null) return;

        _markerRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", on ? _highlightColor : _defaultColor);
        _markerRenderer.SetPropertyBlock(_propertyBlock);
    }

    // ── 占有 ─────────────────────────────────────────────────
    public bool TryOccupy()
    {
        if (_isOccupied) return false;
        _isOccupied = true;
        return true;
    }

    public void Release()
    {
        _isOccupied = false;
        SetHighlight(false);
    }

    // ── ギズモ ────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = _requireIceAxe ? Color.cyan : Color.green;
        Gizmos.DrawWireSphere(transform.position, _grabRadius);
    }
}
