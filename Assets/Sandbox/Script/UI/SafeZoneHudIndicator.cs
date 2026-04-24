using UnityEngine;
using TMPro;

/// <summary>
/// GDD §10.5 — セーフゾーン内にいることを HUD に表示する簡易インジケーター。
///
/// 参照した <see cref="ShelterOccupant"/> の IsSheltered を毎フレーム確認し、
/// 真のときだけラベル / ルートオブジェクトを表示する。
///
/// シーン配置:
///   - HUD Canvas 配下に「SafeZoneLabel」を作成し、本コンポーネントをアタッチ。
///   - _label: "セーフゾーン" テキスト
///   - _root : 非表示時に隠したいラッパー（省略可）
///   - _occupant: ローカルプレイヤーの ShelterOccupant
/// </summary>
[DisallowMultipleComponent]
public class SafeZoneHudIndicator : MonoBehaviour
{
    [Header("監視対象")]
    [SerializeField] private ShelterOccupant _occupant;

    [Header("UI 要素")]
    [SerializeField] private GameObject      _root;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private string          _labelText = "セーフゾーン";

    private bool _lastState;

    private void Awake()
    {
        if (_label != null) _label.text = _labelText;
        Apply(false);
    }

    private void Update()
    {
        bool sheltered = _occupant != null && _occupant.IsSheltered;
        if (sheltered == _lastState) return;
        Apply(sheltered);
    }

    private void Apply(bool visible)
    {
        _lastState = visible;
        if (_root  != null) _root.SetActive(visible);
        else if (_label != null) _label.enabled = visible;
    }

    /// <summary>動的に監視対象を切り替えたい場合（プレイヤーリスポーン時など）。</summary>
    public void SetOccupant(ShelterOccupant occupant)
    {
        _occupant = occupant;
    }
}
