using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// 手続きベースキャンプ（<see cref="BasecampBuilder"/>）の低レベルプリミティブ生成ヘルパー。
    /// 実行時に URP/Lit のマット材質（強い日射 + ACES + 露出での白飛びを避けるため Smoothness 低・アルベドは
    /// 抑えめ）でボックス/シリンダー/板/ライトを組み立てる。マテリアルは色＋質感でキャッシュし共有する
    /// （生成材質はインスタンスなのでアセットや他シーンを汚さない）。
    ///
    /// 当たり判定の方針：
    ///   - solid=true … プレイヤーが触れる構造物（柱・梁・カウンター・木箱・樽・柵・床）。Box/Cyl とも明示サイズの
    ///     BoxCollider を付ける（Cylinder プリミティブ既定の CapsuleCollider は丸端がはみ出すため使わない）。
    ///   - solid=false … 旗・幕・ロープ・炎などの装飾。コライダー無し（プレイヤーに引っかからない）。
    /// </summary>
    internal static class BasecampPropKit
    {
        private static Shader s_lit;
        private static readonly Dictionary<string, Material> s_matCache = new();

        private static Shader Lit => s_lit != null
            ? s_lit
            : (s_lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

        // ── マテリアル ───────────────────────────────────────────
        public static Material Mat(Color baseColor, float smoothness = 0.06f, float metallic = 0f,
                                   Color? emission = null)
        {
            string key = $"{baseColor}|{smoothness:F2}|{metallic:F2}|{emission}";
            if (s_matCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var m = new Material(Lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            else m.color = baseColor;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
            }
            s_matCache[key] = m;
            return m;
        }

        // ── ボックス（Cube プリミティブ） ─────────────────────────
        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size,
                                     Material mat, bool solid = true, Vector3? eulerLocal = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            if (eulerLocal.HasValue) t.localRotation = Quaternion.Euler(eulerLocal.Value);
            t.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (!solid && col != null) Object.Destroy(col); // Cube mesh bounds=1 → BoxCollider は size のまま OK
            return go;
        }

        // ── シリンダー（柱・樽・丸太・円盤） ──────────────────────
        // height は最終的なワールド高さ。Unity の Cylinder メッシュは Y=±1（高さ2）・直径1。
        public static GameObject Cyl(string name, Transform parent, Vector3 localPos, float radius,
                                     float height, Material mat, bool solid = true, Vector3? eulerLocal = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            if (eulerLocal.HasValue) t.localRotation = Quaternion.Euler(eulerLocal.Value);
            t.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var capsule = go.GetComponent<Collider>();
            if (capsule != null) Object.Destroy(capsule); // 既定 CapsuleCollider を捨てる
            if (solid)
            {
                // メッシュ空間でのバウンズ（直径1・高さ2）に合わせた箱で四角近似。
                var bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = new Vector3(1f, 2f, 1f);
            }
            return go;
        }

        // ── 空コンテナ ───────────────────────────────────────────
        public static Transform Group(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        // ── 点光源（焚き火・ランタン） ────────────────────────────
        public static Light PointLight(string name, Transform parent, Vector3 localPos, Color color,
                                       float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None; // 大量の点光源で影を焼かない（負荷対策）
            return l;
        }
    }
}
