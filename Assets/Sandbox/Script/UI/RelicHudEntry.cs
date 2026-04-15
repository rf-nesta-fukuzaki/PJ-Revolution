using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD上の遺物1件を表示するエントリ。
/// </summary>
public class RelicHudEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private Slider          _hpBar;
    [SerializeField] private Image           _conditionIcon;
    [SerializeField] private Color[]         _conditionColors;  // Perfect/Damaged/HeavilyDamaged/Destroyed

    private RelicBase _relic;

    public void Initialize(RelicBase relic)
    {
        _relic = relic;

        if (_nameLabel != null)
            _nameLabel.text = relic.RelicName;
    }

    private void Update()
    {
        if (_relic == null) return;

        if (_hpBar != null)
            _hpBar.value = _relic.HpPercent / 100f;

        if (_conditionIcon != null && _conditionColors != null && _conditionColors.Length >= 4)
        {
            int idx = (int)_relic.Condition;
            _conditionIcon.color = _conditionColors[Mathf.Clamp(idx, 0, _conditionColors.Length - 1)];
        }
    }
}
