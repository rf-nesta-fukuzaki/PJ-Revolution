using UnityEngine;
using TMPro;

/// <summary>リザルト画面のコメディ称号行。</summary>
public class TitleRowEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _playerLabel;
    [SerializeField] private TextMeshProUGUI _titleLabel;

    public void Set(string playerName, string title)
    {
        if (_playerLabel != null) _playerLabel.text = playerName;
        if (_titleLabel  != null) _titleLabel.text  = $"「{title}」";
    }
}
