using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sandbox.UI
{
    public enum VitalIcon { Heart, Bolt }

    /// <summary>
    /// R.E.P.O. 風のバイタルゲージ（体力 / スタミナ）。1本につき
    /// アイコン＋立体的な枠＋暗トラック＋ダメージ残像＋セグメント目盛＋メインフィル＋上部グロス＋数値
    /// を手続き生成（外部アセット不要）。値は <see cref="SetTarget"/> にターゲットを渡すだけで
    /// 内部で滑らかに補間し、ダメージ時は残像が遅れて追従、低残量時はフィルとアイコンがパルスする。
    /// </summary>
    public class VitalsBar : MonoBehaviour
    {
        // ── 見た目の定数（暖色・低彩度の house パレットに調和する暗色基調） ──
        private static readonly Color FrameColor   = new Color(0.06f, 0.07f, 0.09f, 0.98f);
        private static readonly Color TrackColor   = new Color(0.13f, 0.14f, 0.17f, 0.95f);
        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.92f);
        private static readonly Color TrailColor   = new Color(1f, 0.96f, 0.92f, 0.92f); // ダメージで一瞬残る明チップ
        private static readonly Color GlossColor   = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color SegmentColor = new Color(0f, 0f, 0f, 0.32f);
        private static readonly Color NumberColor  = new Color(1f, 0.98f, 0.94f, 0.95f);

        private const float MainLerpSpeed  = 5.5f;  // メインフィルの追従（割合/秒）
        private const float TrailDelay     = 0.35f; // ダメージ後、残像が動き出すまでの待ち
        private const float TrailLerpSpeed = 0.85f; // 残像の追従（割合/秒）
        private const float LowThreshold   = 0.28f; // これ未満でパルス
        private const float PulseHz        = 8.5f;

        // ── 参照 ──
        private Image _trailFill;
        private Image _mainFill;
        private Image _gloss;
        private Image _icon;
        private TextMeshProUGUI _number;
        private float _barW; // フィル幅アニメの基準（先端の角丸を保つため幅で表現する）

        private Color _fullColor;
        private Color _lowColor;
        private bool  _showNumber;

        // ── アニメ状態 ──
        private float _target = 1f;
        private float _main   = 1f;
        private float _trail  = 1f;
        private float _trailTimer;
        private float _displayNumber = 100f;
        private int   _lastShownNumber = -1;
        private bool  _initialized;

        /// <summary>左上アンカー(0,1)の親に1本生成する。topLeftPos は親ローカルの左上座標。</summary>
        public static VitalsBar Create(Transform parent, Vector2 topLeftPos, float width, float barHeight,
            VitalIcon icon, Color full, Color low, bool showNumber)
        {
            float rowHeight = barHeight + 4f; // アイコン＝枠が少しはみ出る分を含めた行の高さ

            var root = new GameObject("VitalsBar", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, rowHeight);
            rt.anchoredPosition = topLeftPos;

            var bar = root.AddComponent<VitalsBar>();
            bar._fullColor  = full;
            bar._lowColor   = low;
            bar._showNumber = showNumber;
            bar.Build(width, barHeight, icon);
            return bar;
        }

        /// <summary>0〜1 の残量ターゲット。毎フレーム呼んでよい（内部で補間）。</summary>
        public void SetTarget(float pct01)
        {
            _target = Mathf.Clamp01(pct01);
            if (!_initialized) { _main = _trail = _target; _initialized = true; }
        }

        /// <summary>数値表示に使う実数値（例: 現在 HP）。</summary>
        public void SetNumber(float value) => _displayNumber = value;

        // ── 構築 ───────────────────────────────────────────────
        private void Build(float width, float barHeight, VitalIcon icon)
        {
            float iconSize = barHeight + 4f;
            float barX     = iconSize + 8f;
            float barW     = width - barX;
            _barW = barW;

            // アイコン（左）
            _icon = MakeImage("Icon", IconSprite(icon), BrightOf(_fullColor));
            _icon.preserveAspect = true;
            PlaceBar(_icon.rectTransform, 0f, iconSize, iconSize);
            AddOutline(_icon.gameObject, 1.5f);

            // 立体的な枠（housing）
            var frame = MakeSliced("Frame", FrameColor);
            PlaceBar(frame.rectTransform, barX - 3f, barW + 6f, barHeight + 6f);
            AddOutline(frame.gameObject, 2f);

            // 暗トラック
            var track = MakeSliced("Track", TrackColor);
            PlaceBar(track.rectTransform, barX, barW, barHeight);

            // ダメージ残像（メインの背後）。先端の角丸を保つため Sliced を幅でアニメする。
            _trailFill = MakeSliced("Trail", TrailColor);
            PlaceBar(_trailFill.rectTransform, barX, barW, barHeight);

            // メインフィル
            _mainFill = MakeSliced("Fill", _fullColor);
            PlaceBar(_mainFill.rectTransform, barX, barW, barHeight);

            // 上部グロス（フィルに追従する光沢）
            _gloss = MakeSliced("Gloss", GlossColor);
            PlaceBar(_gloss.rectTransform, barX, barW, barHeight * 0.42f);
            _gloss.rectTransform.anchoredPosition += new Vector2(0f, barHeight * 0.26f);

            // セグメント目盛（タイル）
            var seg = MakeImage("Segments", SegmentSprite(), Color.white);
            seg.type = Image.Type.Tiled;
            seg.pixelsPerUnitMultiplier = 1f;
            PlaceBar(seg.rectTransform, barX, barW, barHeight);

            // 数値
            if (_showNumber)
            {
                var ngo = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
                ngo.transform.SetParent(transform, false);
                _number = ngo.GetComponent<TextMeshProUGUI>();
                _number.text = "100";
                PlaceBar(_number.rectTransform, barX, barW - 8f, barHeight);
                _number.alignment = TextAlignmentOptions.Right;
                _number.fontSize  = barHeight * 0.70f;
                _number.fontStyle = FontStyles.Bold;
                _number.color = NumberColor;
                _number.raycastTarget = false;
                UiReadability.MakeReadable(_number);
            }
        }

        // ── 毎フレーム補間 ──────────────────────────────────────
        private void Update()
        {
            float dt = Time.deltaTime;

            _main = Mathf.MoveTowards(_main, _target, MainLerpSpeed * dt);

            if (_target >= _trail - 0.0001f)
            {
                // 回復・等値: 残像はメインと一緒に上がる（ダメージ帯は出さない）
                _trail = Mathf.Max(_trail, _main);
                if (_trail > _target) _trail = _target;
                _trail = Mathf.Max(_trail, _main);
                _trailTimer = 0f;
            }
            else
            {
                // ダメージ: 残像はしばらく留まってから、メインへ向けてゆっくり詰める
                _trailTimer += dt;
                if (_trailTimer >= TrailDelay)
                    _trail = Mathf.MoveTowards(_trail, _main, TrailLerpSpeed * dt);
            }
            _trail = Mathf.Max(_trail, _main);

            SetFillWidth(_mainFill,  _main);
            SetFillWidth(_trailFill, _trail);
            SetFillWidth(_gloss,     _main);

            bool  low   = _target < LowThreshold;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseHz) : 0f;

            Color fill = Color.Lerp(_lowColor, _fullColor, _main);
            if (low)
            {
                fill = Color.Lerp(fill, _lowColor, 0.35f);
                fill = Brighten(fill, 0.85f + 0.30f * pulse);
            }
            _mainFill.color = fill;

            Color iconBase = low ? Color.Lerp(_fullColor, _lowColor, 0.55f) : _fullColor;
            _icon.color = BrightOf(iconBase);
            _icon.rectTransform.localScale = Vector3.one * (low ? 1f + 0.14f * pulse : 1f);

            if (_number != null)
            {
                // 変化したフレームだけ ToString（毎フレームの文字列割り当てを避ける）。
                int n = Mathf.Max(0, Mathf.CeilToInt(_displayNumber));
                if (n != _lastShownNumber)
                {
                    _lastShownNumber = n;
                    _number.text = n.ToString();
                }
            }
        }

        // ── 生成ヘルパー ────────────────────────────────────────
        private Image MakeImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Image MakeSliced(string name, Color color)
        {
            var img = MakeImage(name, RoundedSprite(), color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        /// <summary>
        /// フィルの残量を「幅」で表現する（角丸 9 スライスを引き伸ばさないので先端が尖らない）。
        /// 高さ・縦位置は生成時のまま保ち、横幅だけを barW×pct に更新する。
        /// </summary>
        private void SetFillWidth(Image img, float pct)
        {
            pct = Mathf.Clamp01(pct);
            var rt = img.rectTransform;
            var size = rt.sizeDelta;
            size.x = _barW * pct;
            rt.sizeDelta = size;

            bool show = pct > 0.002f;
            if (img.enabled != show) img.enabled = show;
        }

        private static void PlaceBar(RectTransform rt, float x, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        private static void AddOutline(GameObject go, float dist)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = OutlineColor;
            o.effectDistance = new Vector2(dist, dist);
            o.useGraphicAlpha = false;
        }

        private static Color BrightOf(Color c) => Color.Lerp(c, Color.white, 0.30f);

        private static Color Brighten(Color c, float k)
            => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        // ── 手続きスプライト（静的キャッシュ） ─────────────────────
        private static Sprite s_rounded, s_segment, s_heart, s_bolt;

        /// <summary>角丸矩形（9スライス境界付き・AA）。枠/トラック/フィル/グロスで共用。</summary>
        private static Sprite RoundedSprite()
        {
            if (s_rounded != null) return s_rounded;
            const int w = 48, h = 24;
            const float r = 8f, aa = 1.25f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - r), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - r), 0f);
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((r - d) / aa));
                }
            tex.SetPixels(px); tex.Apply();
            s_rounded = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return s_rounded;
        }

        /// <summary>右端に縦線を持つタイル。Image.Type.Tiled で等間隔のセグメント目盛になる。</summary>
        private static Sprite SegmentSprite()
        {
            if (s_segment != null) return s_segment;
            const int w = 24, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);
            var soft = new Color(SegmentColor.r, SegmentColor.g, SegmentColor.b, SegmentColor.a * 0.55f);
            for (int y = 0; y < h; y++)
            {
                px[y * w + (w - 1)] = SegmentColor;
                px[y * w + (w - 2)] = soft;
            }
            tex.SetPixels(px); tex.Apply();
            s_segment = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return s_segment;
        }

        private static Sprite IconSprite(VitalIcon icon)
            => icon == VitalIcon.Heart ? HeartSprite() : BoltSprite();

        private static Sprite HeartSprite()
        {
            if (s_heart != null) return s_heart;
            s_heart = ShapeSprite(40, IsInsideHeart);
            return s_heart;
        }

        private static Sprite BoltSprite()
        {
            if (s_bolt != null) return s_bolt;
            s_bolt = ShapeSprite(40, IsInsideBolt);
            return s_bolt;
        }

        // 古典的なハート陰関数: (x² + y² − 1)³ − x²y³ ≤ 0（y上向き・上に2つの膨らみ、下に尖り）。
        private static bool IsInsideHeart(float nx, float ny)
        {
            float x = nx * 1.30f;
            float y = ny * 1.30f + 0.20f;
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        // 稲妻（単純多角形）を点内包判定で塗る。
        private static readonly Vector2[] s_boltPoly =
        {
            new Vector2( 0.22f,  0.95f), new Vector2(-0.50f,  0.08f), new Vector2(-0.08f,  0.08f),
            new Vector2(-0.22f, -0.95f), new Vector2( 0.52f, -0.06f), new Vector2( 0.10f, -0.06f),
        };

        private static bool IsInsideBolt(float x, float y) => PointInPolygon(x, y, s_boltPoly);

        private static bool PointInPolygon(float x, float y, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if (((p[i].y > y) != (p[j].y > y)) &&
                    (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>[-1,1]×[-1,1] の内包判定述語を 3×3 スーパーサンプルで塗ったアイコンスプライト。</summary>
        private static Sprite ShapeSprite(int size, System.Func<float, float, bool> inside)
        {
            const int ss = 3;
            const float inv = 1f / ss;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float fx = (x + (sx + 0.5f) * inv) / size * 2f - 1f;
                            float fy = (y + (sy + 0.5f) * inv) / size * 2f - 1f;
                            if (inside(fx, fy)) hit++;
                        }
                    px[y * size + x] = new Color(1f, 1f, 1f, (float)hit / (ss * ss));
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
