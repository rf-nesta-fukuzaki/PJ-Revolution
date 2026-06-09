using System.Collections;
using TMPro;
using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>MIMESIS 風 — TIPS テキストを 1 文字ずつ表示。</summary>
    [DisallowMultipleComponent]
    public sealed class LoadingTipTypewriter : MonoBehaviour
    {
        [SerializeField] private float _charsPerSecond = 42f;
        [SerializeField] private float _postDelay = 0.35f;

        private TextMeshProUGUI _target;
        private Coroutine _routine;
        private string _fullText = string.Empty;

        public void Bind(TextMeshProUGUI target) => _target = target;

        public void Show(string text)
        {
            _fullText = text ?? string.Empty;
            if (_routine != null) StopCoroutine(_routine);

            if (!isActiveAndEnabled)
            {
                if (_target != null) _target.text = _fullText;
                return;
            }

            _routine = StartCoroutine(TypeRoutine());
        }

        public void Hide()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_target != null) _target.text = string.Empty;
        }

        private IEnumerator TypeRoutine()
        {
            if (_target == null) yield break;
            _target.text = string.Empty;

            if (string.IsNullOrEmpty(_fullText)) yield break;

            float interval = 1f / Mathf.Max(8f, _charsPerSecond);
            for (int i = 0; i < _fullText.Length; i++)
            {
                _target.text = _fullText.Substring(0, i + 1);
                if (_fullText[i] == '。' || _fullText[i] == '！' || _fullText[i] == '\n')
                    yield return new WaitForSecondsRealtime(interval * 3f);
                else
                    yield return new WaitForSecondsRealtime(interval);
            }
            yield return new WaitForSecondsRealtime(_postDelay);
        }
    }
}
