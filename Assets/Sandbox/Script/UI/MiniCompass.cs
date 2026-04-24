using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §14.3 — ミニコンパス HUD 要素。
///
/// 画面右上に表示される方角インジケーター（N/S/E/W）。
/// プレイヤー視線の向きに応じて文字が回転し、アクティブなピンの方向に矢印アイコンを表示。
///
/// 表示仕様:
///   - 北 (N)     → プレイヤー視線が真北(+Z world)を向いたとき 画面中央上
///   - ピン矢印   → PinSystem.ActivePins を参照し、各ピンの方位を矢印アイコンで表示
///   - アイコン色 → ピン種別（Danger=赤 / Relic=青 / Route=黄）
///
/// 必要参照:
///   - _playerCamera: プレイヤーのカメラ（視線の Y 軸向きを取得）
///   - _dialTransform: N/S/E/W ラベルが並んだコンパスダイヤル（回転対象）
///   - _pinArrowPrefab: ピン矢印 UI プレハブ（Image + 色変更可能）
///   - _pinArrowParent: 矢印プレハブの親（画面上のコンパス表示領域）
/// </summary>
public class MiniCompass : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Transform       _playerCamera;
    [SerializeField] private RectTransform   _dialTransform;
    [SerializeField] private RectTransform   _pinArrowParent;
    [SerializeField] private GameObject      _pinArrowPrefab;

    [Header("表示設定")]
    [Tooltip("ピン矢印を配置するコンパス円の半径（px）")]
    [SerializeField] private float _arrowRadius = 60f;

    [Tooltip("ピン矢印を表示する最大距離（m）。これを超えるピンは非表示")]
    [SerializeField] private float _pinDisplayMaxDistance = 500f;

    // ── ピン種別ごとの色（PinSystem.PinType と対応）──────────
    private static readonly Color[] PIN_COLORS = {
        Color.red,
        new Color(0.1f, 0.4f, 1f),
        new Color(1f, 0.85f, 0f),
    };

    // ピン矢印プール（矢印プレハブを使い回し）
    private readonly List<Image> _arrowPool = new();

    private void LateUpdate()
    {
        if (_playerCamera == null)
        {
            var camGO = GameObject.FindWithTag("MainCamera");
            if (camGO != null) _playerCamera = camGO.transform;
            if (_playerCamera == null) return;
        }

        UpdateDialRotation();
        UpdatePinArrows();
    }

    private void UpdateDialRotation()
    {
        if (_dialTransform == null) return;

        // カメラ前方の Y 軸回転（北=0° から時計回り）
        Vector3 fwd = _playerCamera.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return;

        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        // ダイヤルはプレイヤーの向きと逆方向に回転する（北ラベルが北方向に常に指す）
        _dialTransform.localRotation = Quaternion.Euler(0f, 0f, yaw);
    }

    private void UpdatePinArrows()
    {
        if (_pinArrowParent == null || _pinArrowPrefab == null) return;

        var pins = PinSystem.ActivePins;
        int visibleCount = 0;
        Vector3 playerPos = _playerCamera.position;
        Vector3 fwd = _playerCamera.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return;
        float playerYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

        for (int i = 0; i < pins.Count; i++)
        {
            var pin = pins[i];
            if (pin.transform == null) continue;

            Vector3 delta = pin.transform.position - playerPos;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            if (distance > _pinDisplayMaxDistance) continue;

            float pinYaw  = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float relAng  = Mathf.DeltaAngle(playerYaw, pinYaw);

            var arrow = GetOrCreateArrow(visibleCount);
            arrow.color = PIN_COLORS[Mathf.Clamp((int)pin.type, 0, PIN_COLORS.Length - 1)];

            var rt = arrow.rectTransform;
            float rad = relAng * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad) * _arrowRadius, Mathf.Cos(rad) * _arrowRadius);
            rt.localRotation    = Quaternion.Euler(0f, 0f, -relAng);
            rt.gameObject.SetActive(true);

            visibleCount++;
        }

        // 未使用の矢印を非表示
        for (int i = visibleCount; i < _arrowPool.Count; i++)
        {
            if (_arrowPool[i] != null && _arrowPool[i].gameObject != null)
                _arrowPool[i].gameObject.SetActive(false);
        }
    }

    private Image GetOrCreateArrow(int index)
    {
        while (_arrowPool.Count <= index)
        {
            var go = Instantiate(_pinArrowPrefab, _pinArrowParent);
            var img = go.GetComponent<Image>();
            _arrowPool.Add(img);
        }
        return _arrowPool[index];
    }
}
