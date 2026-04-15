using UnityEngine;

/// <summary>
/// GDD §7.1 L2 — ルート開閉ゲート。
/// 橋・洞窟の通行可否・ショートカットのランダム制御に使用する。
/// </summary>
public class RouteGate : MonoBehaviour
{
    [Header("ゲート設定")]
    [SerializeField] private string      _routeName        = "Route A";
    [SerializeField] private bool        _defaultOpen      = true;

    [Header("ブロッカー（閉じたときに有効化するオブジェクト）")]
    [SerializeField] private GameObject[] _blockers;

    [Header("開放時の演出")]
    [SerializeField] private ParticleSystem _openParticles;

    private bool _isOpen;

    public bool IsOpen   => _isOpen;
    public string Name   => _routeName;

    private void Start()
    {
        SetOpen(_defaultOpen);
    }

    public void SetOpen(bool open)
    {
        _isOpen = open;

        foreach (var blocker in _blockers)
        {
            if (blocker != null)
                blocker.SetActive(!open);
        }

        if (open && _openParticles != null)
            _openParticles.Play();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _defaultOpen ? Color.green : Color.red;
        Gizmos.DrawCube(transform.position, Vector3.one * 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}
