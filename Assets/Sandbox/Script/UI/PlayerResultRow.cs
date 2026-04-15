using UnityEngine;
using TMPro;

/// <summary>リザルト画面の個人スコア行。</summary>
public class PlayerResultRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private TextMeshProUGUI _scoreLabel;
    [SerializeField] private TextMeshProUGUI _detailLabel;

    public void Populate(PlayerScore ps)
    {
        if (_nameLabel  != null) _nameLabel.text  = ps.PlayerName;
        if (_scoreLabel != null) _scoreLabel.text = $"{ps.IndividualScore} pt";
        if (_detailLabel != null)
            _detailLabel.text = $"転落 {ps.FallCount}回  遺物ダメージ {ps.RelicDamageDealt:F0}  ロープ設置 {ps.RopePlacementCount}";
    }
}
