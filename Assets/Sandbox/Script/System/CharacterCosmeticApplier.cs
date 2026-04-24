using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §9.5 — キャラクタービジュアルコスメティック適用コンポーネント。
/// CosmeticManager のアンロック状態を読み取り、プレイヤーの外見を変更する。
/// 外部アセット不要。URP/Lit マテリアルとプリミティブ形状で表現する。
///
/// 使い方:
///   1. PlayerPrefab のルートにアタッチ
///   2. _bodyRenderer に体の Renderer をアサイン
///   3. _headAttachPoint に頭のボーン/Transform をアサイン
///   4. _backAttachPoint に背中の Transform をアサイン
///   5. BasecampShop / CosmeticSelectionUI から SetSkin/SetHat/SetPack を呼ぶ
/// </summary>
public class CharacterCosmeticApplier : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material s_sharedCosmeticMaterial;

    private static class CosmeticIds
    {
        public const string SkinDefault = "skin_default";
        public const string SkinSnowman = "skin_snowman";
        public const string SkinNinja = "skin_ninja";
        public const string HatBeanie = "hat_beanie";
        public const string HatHardhat = "hat_hardhat";
        public const string HatExplorer = "hat_explorer";
        public const string PackStandard = "pack_standard";
        public const string PackLarge = "pack_large";
        public const string PackExpedition = "pack_expedition";
    }

    [Header("ビジュアルルーツ（Inspector でアサイン）")]
    [SerializeField] private Renderer  _bodyRenderer;      // メインボディの Renderer
    [SerializeField] private Transform _headAttachPoint;   // ハット取り付け点
    [SerializeField] private Transform _backAttachPoint;   // バックパック取り付け点

    // ── 現在装備中 ──────────────────────────────────────────
    private string _activeSkinId = CosmeticIds.SkinDefault;
    private string _activeHatId  = CosmeticIds.HatBeanie;
    private string _activePackId = CosmeticIds.PackStandard;

    // 動的生成オブジェクトキャッシュ
    private GameObject _currentHatObj;
    private GameObject _currentPackObj;
    private Material _runtimeBodyMaterial;

    // ── スキン色定義 ─────────────────────────────────────────
    private static readonly Dictionary<string, Color> SKIN_COLORS = new()
    {
        [CosmeticIds.SkinDefault] = new Color(0.85f, 0.70f, 0.45f),
        [CosmeticIds.SkinSnowman] = new Color(0.95f, 0.95f, 0.98f),
        [CosmeticIds.SkinNinja] = new Color(0.10f, 0.10f, 0.12f),
    };

    // ── ライフサイクル ────────────────────────────────────────
    private void Start()
    {
        if (GameServices.Cosmetics != null)
            GameServices.Cosmetics.OnCosmeticUnlocked += OnCosmeticUnlocked;

        ApplyAllCosmetics();
    }

    private void OnDestroy()
    {
        if (GameServices.Cosmetics != null)
            GameServices.Cosmetics.OnCosmeticUnlocked -= OnCosmeticUnlocked;

        if (_runtimeBodyMaterial != null)
            Destroy(_runtimeBodyMaterial);
    }

    private void OnCosmeticUnlocked(string id)
    {
        // 新規アンロック通知（自動装備はしない）
        Debug.Log($"[CosmeticApplier] アンロック通知: {id}");
    }

    // ── 外部 API ─────────────────────────────────────────────
    /// <summary>スキン（ボディカラー）を変更する。</summary>
    public void SetSkin(string skinId)
    {
        if (!ValidateUnlocked(skinId)) return;
        _activeSkinId = skinId;
        ApplySkin(skinId);
    }

    /// <summary>ハットを変更する。</summary>
    public void SetHat(string hatId)
    {
        if (!ValidateUnlocked(hatId)) return;
        _activeHatId = hatId;
        RebuildHat(hatId);
    }

    /// <summary>バックパックを変更する。</summary>
    public void SetPack(string packId)
    {
        if (!ValidateUnlocked(packId)) return;
        _activePackId = packId;
        RebuildPack(packId);
    }

    public string ActiveSkinId => _activeSkinId;
    public string ActiveHatId  => _activeHatId;
    public string ActivePackId => _activePackId;

    /// <summary>保存済みのコスメティックを全て再適用する（シーンロード後などに呼ぶ）。</summary>
    public void ApplyAllCosmetics()
    {
        ApplySkin(_activeSkinId);
        RebuildHat(_activeHatId);
        RebuildPack(_activePackId);
    }

    // ── スキン適用 ────────────────────────────────────────────
    private void ApplySkin(string skinId)
    {
        if (_bodyRenderer == null) return;

        if (!SKIN_COLORS.TryGetValue(skinId, out var color))
            color = SKIN_COLORS[CosmeticIds.SkinDefault];

        var mat = GetOrCreateBodyMaterial();
        if (mat == null) return;

        mat.color = color;

        if (skinId == CosmeticIds.SkinNinja)
        {
            // 忍者スキン：金属光沢
            SetMatFloat(mat, "_Metallic",   0.7f);
            SetMatFloat(mat, "_Smoothness", 0.8f);
        }
        else
        {
            SetMatFloat(mat, "_Metallic",   0.0f);
            SetMatFloat(mat, "_Smoothness", 0.3f);
        }
    }

    private static void SetMatFloat(Material mat, string prop, float val)
    {
        if (mat.HasProperty(prop)) mat.SetFloat(prop, val);
    }

    private Material GetOrCreateBodyMaterial()
    {
        if (_runtimeBodyMaterial != null) return _runtimeBodyMaterial;

        var source = _bodyRenderer.sharedMaterial;
        if (source != null)
            _runtimeBodyMaterial = new Material(source);
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;
            _runtimeBodyMaterial = new Material(shader);
        }

        _runtimeBodyMaterial.name = "CharacterBodyRuntimeMaterial";
        _bodyRenderer.material = _runtimeBodyMaterial;
        return _runtimeBodyMaterial;
    }

    // ── ハット ────────────────────────────────────────────────
    private void RebuildHat(string hatId)
    {
        if (_currentHatObj != null)
        {
            Destroy(_currentHatObj);
            _currentHatObj = null;
        }

        if (_headAttachPoint == null) return;
        _currentHatObj = BuildHatPrimitive(hatId, _headAttachPoint);
    }

    private static GameObject BuildHatPrimitive(string hatId, Transform parent)
    {
        var root = new GameObject($"Hat_{hatId}");
        root.transform.SetParent(parent, worldPositionStays: false);
        root.transform.localPosition = Vector3.zero;

        switch (hatId)
        {
            case CosmeticIds.HatBeanie:
                // ニット帽：丸みのある半球
                AddPrimChild(root, PrimitiveType.Sphere, "dome",
                    new Vector3(0f, 0.14f, 0f),
                    new Vector3(0.56f, 0.38f, 0.56f),
                    new Color(0.20f, 0.45f, 0.80f));
                break;

            case CosmeticIds.HatHardhat:
                // ヘルメット：ドーム + 鍔
                AddPrimChild(root, PrimitiveType.Sphere, "dome",
                    new Vector3(0f, 0.20f, 0f),
                    new Vector3(0.64f, 0.52f, 0.64f),
                    new Color(0.95f, 0.85f, 0.10f));
                AddPrimChild(root, PrimitiveType.Cylinder, "brim",
                    new Vector3(0f, 0.05f, 0f),
                    new Vector3(0.80f, 0.06f, 0.80f),
                    new Color(0.88f, 0.78f, 0.08f));
                break;

            case CosmeticIds.HatExplorer:
                // 探検家帽：クラウン + 広い鍔
                AddPrimChild(root, PrimitiveType.Cylinder, "crown",
                    new Vector3(0f, 0.24f, 0f),
                    new Vector3(0.52f, 0.36f, 0.52f),
                    new Color(0.55f, 0.38f, 0.18f));
                AddPrimChild(root, PrimitiveType.Cylinder, "brim",
                    new Vector3(0f, 0.05f, 0f),
                    new Vector3(0.84f, 0.06f, 0.84f),
                    new Color(0.48f, 0.33f, 0.15f));
                break;

            default:
                // 未知または "none" → 何もスポーンしない
                break;
        }

        return root;
    }

    // ── バックパック ──────────────────────────────────────────
    private void RebuildPack(string packId)
    {
        if (_currentPackObj != null)
        {
            Destroy(_currentPackObj);
            _currentPackObj = null;
        }

        if (_backAttachPoint == null) return;
        _currentPackObj = BuildPackPrimitive(packId, _backAttachPoint);
    }

    private static GameObject BuildPackPrimitive(string packId, Transform parent)
    {
        var root = new GameObject($"Pack_{packId}");
        root.transform.SetParent(parent, worldPositionStays: false);
        root.transform.localPosition = Vector3.zero;

        var packColor    = new Color(0.35f, 0.28f, 0.20f);
        var pouchColor   = new Color(0.28f, 0.22f, 0.15f);

        float scaleY = packId switch
        {
            CosmeticIds.PackStandard => 0.55f,
            CosmeticIds.PackLarge => 0.75f,
            CosmeticIds.PackExpedition => 0.95f,
            _                 => 0.55f
        };

        AddPrimChild(root, PrimitiveType.Cube, "body",
            Vector3.zero,
            new Vector3(0.40f, scaleY, 0.20f),
            packColor);

        if (packId == CosmeticIds.PackLarge || packId == CosmeticIds.PackExpedition)
        {
            // サイドポーチ × 2
            AddPrimChild(root, PrimitiveType.Cube, "pouchL",
                new Vector3(-0.28f, 0.10f, 0f),
                new Vector3(0.16f, 0.36f, 0.18f),
                pouchColor);
            AddPrimChild(root, PrimitiveType.Cube, "pouchR",
                new Vector3( 0.28f, 0.10f, 0f),
                new Vector3(0.16f, 0.36f, 0.18f),
                pouchColor);
        }

        if (packId == CosmeticIds.PackExpedition)
        {
            // 上部ロールトップ
            AddPrimChild(root, PrimitiveType.Cylinder, "rolltop",
                new Vector3(0f, scaleY * 0.5f + 0.16f, 0f),
                new Vector3(0.38f, 0.18f, 0.18f),
                pouchColor);
        }

        return root;
    }

    // ── プリミティブ生成ヘルパー ──────────────────────────────
    private static void AddPrimChild(
        GameObject parent,
        PrimitiveType type,
        string childName,
        Vector3 localPos,
        Vector3 localScale,
        Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = childName;
        go.transform.SetParent(parent.transform, worldPositionStays: false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = GetSharedCosmeticMaterial();
            if (mat != null)
                rend.sharedMaterial = mat;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            rend.SetPropertyBlock(block);
        }

        // ハット・パックにコライダーは不要
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    // ── バリデーション ────────────────────────────────────────
    private static bool ValidateUnlocked(string id)
    {
        if (GameServices.Cosmetics == null) return true;
        if (GameServices.Cosmetics.IsUnlocked(id)) return true;

        Debug.LogWarning($"[CosmeticApplier] '{id}' は未アンロックです（Set を無視）");
        return false;
    }

    private static Material GetSharedCosmeticMaterial()
    {
        if (s_sharedCosmeticMaterial != null) return s_sharedCosmeticMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_sharedCosmeticMaterial = new Material(shader)
        {
            name = "CharacterCosmeticSharedMaterial"
        };
        return s_sharedCosmeticMaterial;
    }
}
