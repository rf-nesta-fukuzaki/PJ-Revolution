using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandbox.UI;

/// <summary>
/// GDD §14.3 — ミニコンパス HUD 要素（地図式 / N 上固定）。
///
/// 画面右上の方角インジケーター。N/E/S/W は常に固定（N が上 = 真北）で正立し、
/// 中央の針がプレイヤーの向いている方向を指して回転する。
/// アクティブなピンは絶対方位の位置に矢印で表示する。
///
/// カメラ参照は堅牢に解決する:
///   プレイヤー配下の追従カメラ → Camera.main → 任意の有効カメラ の順。
///   オフラインシーンでは残置 MainCamera が実行時に無効化・再タグされるため、
///   毎フレーム参照の有効性を検証し、無効になったら再解決する（凍結バグの回避）。
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
    [SerializeField] private float _arrowRadius = 44f;

    [Tooltip("ピン矢印を表示する最大距離（m）。これを超えるピンは非表示")]
    [SerializeField] private float _pinDisplayMaxDistance = 500f;

    [Tooltip("コンパス面（背景円・リング・方位・針）を実行時に自動整備する")]
    [SerializeField] private bool _autoBuildFace = true;

    [Tooltip("コンパス全体の表示倍率。1=従来サイズ。HUD の占有を抑えるため既定で縮小。")]
    [Range(0.4f, 1.2f)]
    [SerializeField] private float _faceScale = 0.68f;

    // ── ピン種別ごとの色（PinSystem.PinType と対応）──────────
    private static readonly Color[] PIN_COLORS = {
        new Color(0.94f, 0.33f, 0.30f, 1f), // Danger
        new Color(0.32f, 0.62f, 1f,   1f),  // Relic
        new Color(0.98f, 0.80f, 0.32f, 1f), // Route
    };

    private static readonly string[] Cardinals = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };

    private readonly List<Image> _arrowPool = new();
    private RectTransform _needlePivot;
    private TextMeshProUGUI _headingLabel;

    private static Sprite s_discSprite;
    private static Sprite s_ringSprite;
    private static Sprite s_triangleSprite;

    private void Start()
    {
        if (_autoBuildFace)
            EnsureFaceVisuals();
    }

    private void LateUpdate()
    {
        if (!IsCameraValid(_playerCamera))
            _playerCamera = ResolveCameraTransform();
        if (_playerCamera == null) return;

        if (!TryGetPlayerYaw(out float playerYaw)) return;

        UpdateNeedle(playerYaw);
        UpdateHeadingLabel(playerYaw);
        UpdatePinArrows();
        UpdateFlareMarkers();
    }

    // ── カメラ解決 ───────────────────────────────────────────
    private static bool IsCameraValid(Transform t)
    {
        if (t == null) return false;
        if (!t.gameObject.activeInHierarchy) return false;
        var cam = t.GetComponent<Camera>();
        return cam == null || cam.enabled;
    }

    private static Transform ResolveCameraTransform()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var cam = player.GetComponentInChildren<Camera>(false);
            if (cam != null && cam.enabled) return cam.transform;
        }

        if (Camera.main != null) return Camera.main.transform;

        var cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled)
                return cameras[i].transform;
        }
        return null;
    }

    private bool TryGetPlayerYaw(out float yaw)
    {
        yaw = 0f;
        Vector3 fwd = _playerCamera.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return false;
        yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        return true;
    }

    // ── 針（向き表示）────────────────────────────────────────
    private void UpdateNeedle(float playerYaw)
    {
        if (_needlePivot == null) return;
        // N=上(+Y)。北向き(yaw0)で上、東向き(yaw90)で右を指すよう CW に -yaw 回す。
        _needlePivot.localRotation = Quaternion.Euler(0f, 0f, -playerYaw);
    }

    private int _lastBearingDeg = int.MinValue;

    private void UpdateHeadingLabel(float playerYaw)
    {
        if (_headingLabel == null) return;
        float bearing = (playerYaw + 360f) % 360f;
        int bearingDeg = Mathf.RoundToInt(bearing) % 360;
        // 丸めた方位角（度）が変わったときだけ文字列を再生成し、TMP のメッシュ再構築と
        // 毎フレームの文字列ヒープ確保を避ける（静止時は確保ゼロ）。
        if (bearingDeg == _lastBearingDeg) return;
        _lastBearingDeg = bearingDeg;
        int cardinalIndex = Mathf.RoundToInt(bearing / 45f) % 8;
        _headingLabel.text = $"{Cardinals[cardinalIndex]}  {bearingDeg}°";
    }

    // ── ピン矢印（絶対方位）──────────────────────────────────
    private void UpdatePinArrows()
    {
        if (_pinArrowParent == null || _pinArrowPrefab == null) return;

        var pins = PinSystem.ActivePins;
        int visibleCount = 0;
        Vector3 playerPos = _playerCamera.position;

        for (int i = 0; i < pins.Count; i++)
        {
            var pin = pins[i];
            if (pin.transform == null) continue;

            Vector3 delta = pin.transform.position - playerPos;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            if (distance > _pinDisplayMaxDistance) continue;

            float pinYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

            var arrow = GetOrCreateArrow(visibleCount);
            arrow.color = PIN_COLORS[Mathf.Clamp((int)pin.type, 0, PIN_COLORS.Length - 1)];

            var rt = arrow.rectTransform;
            float rad = pinYaw * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad) * _arrowRadius, Mathf.Cos(rad) * _arrowRadius);
            rt.localRotation = Quaternion.Euler(0f, 0f, -pinYaw);
            rt.gameObject.SetActive(true);

            visibleCount++;
        }

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

    private void UpdateFlareMarkers()
    {
        if (_pinArrowParent == null || _playerCamera == null) return;

        const int flareBase = 100;
        int shown = 0;
        Vector3 playerPos = _playerCamera.position;

        foreach (var flare in FlareBehavior.GetVisibleFlaresFrom(playerPos))
        {
            if (flare == null) continue;

            Vector3 delta = flare.transform.position - playerPos;
            float pinYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

            var arrow = GetOrCreateArrow(flareBase + shown);
            arrow.color = new Color(1f, 0.45f, 0f, 1f);

            var rt = arrow.rectTransform;
            float rad = pinYaw * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad) * _arrowRadius, Mathf.Cos(rad) * _arrowRadius);
            rt.localRotation = Quaternion.Euler(0f, 0f, -pinYaw);
            rt.gameObject.SetActive(true);
            shown++;
        }

        for (int i = shown; i < 24; i++)
        {
            int idx = flareBase + i;
            if (idx < _arrowPool.Count && _arrowPool[idx] != null)
                _arrowPool[idx].gameObject.SetActive(false);
        }
    }

    // ── コンパス面の整備（実行時・冪等）──────────────────────
    private void EnsureFaceVisuals()
    {
        var root = transform as RectTransform;
        if (root == null) return;

        // HUD の占有を抑えるため、ベイク済み・実行時生成の両パーツをまとめて縮小する。
        root.localScale = new Vector3(_faceScale, _faceScale, 1f);

        if (_dialTransform == null)
            BuildDial(root);

        // 方位ラベルは常に正立（ダイヤルは回さない）。残った回転を初期化する。
        if (_dialTransform != null)
            _dialTransform.localRotation = Quaternion.identity;

        EnsureBackgroundDisc(root);
        StyleCardinalLabels();
        EnsureNeedle(root);
        EnsureHeadingLabel(root);
    }

    private void BuildDial(RectTransform root)
    {
        var dialGo = new GameObject("Dial", typeof(RectTransform));
        dialGo.transform.SetParent(root, false);
        _dialTransform = (RectTransform)dialGo.transform;
        _dialTransform.anchorMin = _dialTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _dialTransform.sizeDelta = new Vector2(140f, 140f);
        _dialTransform.anchoredPosition = Vector2.zero;

        CreateCardinal("N", "N", new Vector2(0.5f, 1f), new Vector2(0f, -10f));
        CreateCardinal("E", "E", new Vector2(1f, 0.5f), new Vector2(-10f, 0f));
        CreateCardinal("S", "S", new Vector2(0.5f, 0f), new Vector2(0f, 10f));
        CreateCardinal("W", "W", new Vector2(0f, 0.5f), new Vector2(10f, 0f));

        var arrowParentGo = new GameObject("PinArrows", typeof(RectTransform));
        arrowParentGo.transform.SetParent(root, false);
        _pinArrowParent = (RectTransform)arrowParentGo.transform;
        _pinArrowParent.anchorMin = _pinArrowParent.anchorMax = new Vector2(0.5f, 0.5f);
        _pinArrowParent.sizeDelta = new Vector2(140f, 140f);
        _pinArrowParent.anchoredPosition = Vector2.zero;

        if (_pinArrowPrefab == null)
        {
            var prefab = new GameObject("PinArrowPrefab", typeof(RectTransform), typeof(Image));
            prefab.transform.SetParent(root, false);
            var img = prefab.GetComponent<Image>();
            img.sprite = GetTriangleSprite();
            img.color = Color.white;
            var rt = (RectTransform)prefab.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(12f, 14f);
            prefab.SetActive(false);
            _pinArrowPrefab = prefab;
        }
    }

    private void CreateCardinal(string name, string text, Vector2 anchor, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_dialTransform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(24f, 24f);
        rt.anchoredPosition = anchoredPos;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = UiPalette.Cream;
        tmp.raycastTarget = false;
    }

    private void EnsureBackgroundDisc(RectTransform root)
    {
        if (root.Find("CompassFace") != null) return;

        var disc = new GameObject("CompassFace", typeof(RectTransform), typeof(Image));
        disc.transform.SetParent(root, false);
        var drt = (RectTransform)disc.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(150f, 150f);
        drt.anchoredPosition = Vector2.zero;
        var dimg = disc.GetComponent<Image>();
        dimg.sprite = GetDiscSprite();
        dimg.color = new Color(0.06f, 0.07f, 0.09f, 0.62f);
        dimg.raycastTarget = false;
        disc.transform.SetAsFirstSibling();

        var ring = new GameObject("CompassRing", typeof(RectTransform), typeof(Image));
        ring.transform.SetParent(root, false);
        var rrt = (RectTransform)ring.transform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(150f, 150f);
        rrt.anchoredPosition = Vector2.zero;
        var rimg = ring.GetComponent<Image>();
        rimg.sprite = GetRingSprite();
        rimg.color = new Color(UiPalette.Amber.r, UiPalette.Amber.g, UiPalette.Amber.b, 0.5f);
        rimg.raycastTarget = false;
        ring.transform.SetSiblingIndex(1);
    }

    private void StyleCardinalLabels()
    {
        if (_dialTransform == null) return;
        StyleCardinal("N", UiPalette.Amber, 22, FontStyles.Bold);
        StyleCardinal("E", UiPalette.Cream, 18, FontStyles.Normal);
        StyleCardinal("S", UiPalette.Cream, 18, FontStyles.Normal);
        StyleCardinal("W", UiPalette.Cream, 18, FontStyles.Normal);
    }

    private void StyleCardinal(string name, Color color, int fontSize, FontStyles style)
    {
        var child = _dialTransform.Find(name);
        if (child == null) return;
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        UiReadability.MakeReadable(tmp);
    }

    /// <summary>
    /// 向きを示す針を生成する。赤い先端 = 視線方向、淡色の尾 = 後方。
    /// _needlePivot を回転させて方向を表す（方位ラベルは固定のまま）。
    /// フォント字形に依存しないよう画像（手続き三角形）で描画する。
    /// </summary>
    private void EnsureNeedle(RectTransform root)
    {
        var existing = root.Find("CompassNeedle");
        if (existing != null)
        {
            _needlePivot = (RectTransform)existing;
            return;
        }

        var pivotGo = new GameObject("CompassNeedle", typeof(RectTransform));
        pivotGo.transform.SetParent(root, false);
        _needlePivot = (RectTransform)pivotGo.transform;
        _needlePivot.anchorMin = _needlePivot.anchorMax = new Vector2(0.5f, 0.5f);
        _needlePivot.sizeDelta = new Vector2(40f, 100f);
        _needlePivot.anchoredPosition = Vector2.zero;

        CreateNeedleHalf("Tail", new Color(0.85f, 0.86f, 0.9f, 0.95f), 180f, 32f, 11f);
        CreateNeedleHalf("Tip",  new Color(0.95f, 0.33f, 0.30f, 1f),    0f, 46f, 14f);

        var cap = new GameObject("Cap", typeof(RectTransform), typeof(Image));
        cap.transform.SetParent(_needlePivot, false);
        var crt = (RectTransform)cap.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(11f, 11f);
        crt.anchoredPosition = Vector2.zero;
        var cimg = cap.GetComponent<Image>();
        cimg.sprite = GetDiscSprite();
        cimg.color = UiPalette.Amber;
        cimg.raycastTarget = false;
    }

    private void CreateNeedleHalf(string name, Color color, float zRotation, float length, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_needlePivot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f); // 中心から先端へ伸ばす
        rt.sizeDelta = new Vector2(width, length);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, zRotation);

        var img = go.GetComponent<Image>();
        img.sprite = GetTriangleSprite();
        img.color = color;
        img.raycastTarget = false;
    }

    private void EnsureHeadingLabel(RectTransform root)
    {
        var existing = root.Find("HeadingLabel");
        if (existing != null)
        {
            _headingLabel = existing.GetComponent<TextMeshProUGUI>();
            return;
        }

        var go = new GameObject("HeadingLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 26f);
        rt.anchoredPosition = new Vector2(0f, -94f);
        _headingLabel = go.GetComponent<TextMeshProUGUI>();
        _headingLabel.text = "北  0°";
        _headingLabel.fontSize = 18;
        _headingLabel.alignment = TextAlignmentOptions.Center;
        _headingLabel.color = UiPalette.Cream;
        _headingLabel.raycastTarget = false;
        UiReadability.MakeReadable(_headingLabel);
    }

    // ── 手続きスプライト ─────────────────────────────────────
    private static Sprite GetDiscSprite()
    {
        if (s_discSprite != null) return s_discSprite;

        const int size = 64;
        const float aa = 1.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        float r = c - 1f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((r - d) / aa));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        s_discSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_discSprite;
    }

    /// <summary>上向きの二等辺三角形スプライト（針・矢印用）。頂点が上(+Y)。</summary>
    private static Sprite GetTriangleSprite()
    {
        if (s_triangleSprite != null) return s_triangleSprite;

        const int size = 64;
        const float aa = 1.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float t = y / (float)(size - 1);
            float halfWidth = c * (1f - t);
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((halfWidth - dx) / aa));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        s_triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_triangleSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (s_ringSprite != null) return s_ringSprite;

        const int size = 64;
        const float aa = 1.2f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        float outer = c - 1f;
        float inner = outer - 3f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float aOuter = Mathf.Clamp01((outer - d) / aa);
                float aInner = Mathf.Clamp01((d - inner) / aa);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Min(aOuter, aInner));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        s_ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_ringSprite;
    }
}
