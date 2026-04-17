using UnityEngine;

/// <summary>
/// オフラインテスト用 NPC スポナー。
///
/// シーン起動時に 1〜3 体の AI コンパニオンを動的生成する。
/// NetworkBehaviour に依存しないため、NGO なしで動作する。
///
/// Explorer モデルの設定方法:
///   Inspector の "Explorer Model Prefab" に Assets/Sandbox/Model/Explorer.fbx を
///   ドラッグして設定する。未設定の場合はカプセル+球体のプリミティブで代替する。
///
/// モデルのオフセット:
///   CapsuleCollider の center=Vector3.zero / height=1.8 に合わせ、
///   モデルルートを Y=-0.9 にオフセットしてモデルの足元を底面に揃える。
/// </summary>
public class OfflineNPCSpawner : MonoBehaviour
{
    [Header("NPC 数（1〜3）")]
    [SerializeField, Range(1, 3)] private int _npcCount = 3;

    [Header("スポーン位置")]
    [SerializeField] private Vector3 _spawnBase    = new Vector3(-2f, 1.3f, 0f);
    [SerializeField] private float   _spawnSpacing = 2.5f;

    [Header("NPC 名（空欄でデフォルト名を使用）")]
    [SerializeField] private string[] _npcNames = { "Alex", "Jordan", "Riley" };

    [Header("Explorer モデル設定")]
    [Tooltip("Assets/Sandbox/Model/Explorer.fbx をドラッグして設定してください")]
    [SerializeField] private GameObject _explorerModelPrefab;

    [Tooltip("モデルのローカルオフセット（カプセル底面に足元を合わせる）")]
    [SerializeField] private Vector3 _modelOffset = new Vector3(0f, -0.9f, 0f);

    [Tooltip("モデルのスケール調整")]
    [SerializeField] private Vector3 _modelScale = Vector3.one;

    [Header("NPC カラー（プリミティブ代替時のみ使用）")]
    [SerializeField] private Color[] _npcColors =
    {
        new Color(0.25f, 0.55f, 1.00f),
        new Color(0.20f, 0.80f, 0.30f),
        new Color(0.95f, 0.45f, 0.10f),
    };

    [Header("アニメーター設定")]
    [Tooltip("ExplorerAnimator.controller を設定してください（モデル使用時に自動適用）")]
    [SerializeField] private RuntimeAnimatorController _animatorController;

    // ── ライフサイクル ────────────────────────────────────────
    private void Start()
    {
        TryAutoLoadAssets();

        int count = Mathf.Clamp(_npcCount, 1, 3);
        for (int i = 0; i < count; i++)
            SpawnNPC(i);
    }

    // Editor 実行時に Animator Controller を自動ロード（Inspector 未設定の場合の保険）
    private void TryAutoLoadAssets()
    {
#if UNITY_EDITOR
        if (_animatorController == null)
        {
            _animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Sandbox/Animation/Explorer/ExplorerAnimator.controller");
            if (_animatorController != null)
                Debug.Log("[OfflineNPCSpawner] ExplorerAnimator を自動ロードしました。");
        }
#endif
        if (_animatorController == null)
            Debug.LogWarning("[OfflineNPCSpawner] AnimatorController が未設定です。NPC のアニメーションは再生されません。");

        if (_explorerModelPrefab == null)
            Debug.LogWarning("[OfflineNPCSpawner] Explorer モデルプレハブが未設定です。プリミティブで代替します。");
    }

    // ── NPC 生成 ──────────────────────────────────────────────
    private void SpawnNPC(int index)
    {
        string npcName = ResolveNpcName(index);

        // ルートオブジェクト
        var root = new GameObject($"NPC_{npcName}");
        root.transform.position = _spawnBase + Vector3.right * (index * _spawnSpacing);

        // 物理コンポーネント
        var col    = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.4f;
        col.center = Vector3.zero;

        var rb = root.AddComponent<Rigidbody>();
        rb.freezeRotation         = true;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // AI コントローラー
        root.AddComponent<NPCController>();

        // Explorer モデルを設定する（未設定ならプリミティブで代替）
        if (_explorerModelPrefab != null)
        {
            AttachExplorerModel(root.transform);
        }
        else
        {
            AttachPrimitiveVisual(root.transform, ResolveNpcColor(index));
            // プリミティブ代替時も Animator を root に追加して NPCController が駆動できるようにする
            if (_animatorController != null)
            {
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = _animatorController;
                animator.applyRootMotion = false;
                animator.cullingMode     = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        Debug.Log($"[OfflineNPC] {npcName} をスポーン: {root.transform.position}" +
                  (_explorerModelPrefab != null ? " [Explorer モデル]" : " [プリミティブ代替]"));
    }

    // ── Explorer モデルをアタッチ ─────────────────────────────
    private void AttachExplorerModel(Transform parent)
    {
        var modelGo = Instantiate(_explorerModelPrefab, parent);
        modelGo.name = "ExplorerModel";
        modelGo.transform.localPosition = _modelOffset;
        modelGo.transform.localRotation = Quaternion.identity;
        modelGo.transform.localScale    = _modelScale;

        // 子のコライダーを全て削除（親の CapsuleCollider と競合しないよう）
        foreach (var col in modelGo.GetComponentsInChildren<Collider>())
            Destroy(col);

        // Animator を設定
        SetupAnimator(modelGo);
    }

    // ── Animator のセットアップ ───────────────────────────────
    private void SetupAnimator(GameObject modelGo)
    {
        var animator = modelGo.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = modelGo.AddComponent<Animator>();

        if (_animatorController != null)
            animator.runtimeAnimatorController = _animatorController;

        // Avatar は FBX に含まれている場合そのまま使用される
        animator.applyRootMotion = false;
        animator.cullingMode     = AnimatorCullingMode.CullUpdateTransforms;
    }

    // ── プリミティブ代替ビジュアル ────────────────────────────
    private void AttachPrimitiveVisual(Transform parent, Color bodyColor)
    {
        BuildPrimitive(
            PrimitiveType.Capsule,
            parent,
            localPos:   Vector3.zero,
            localScale: new Vector3(0.8f, 0.9f, 0.8f),
            color:      bodyColor);

        BuildPrimitive(
            PrimitiveType.Sphere,
            parent,
            localPos:   new Vector3(0f, 1.1f, 0f),
            localScale: Vector3.one * 0.35f,
            color:      Color.Lerp(bodyColor, Color.white, 0.45f));
    }

    // ── 名前解決 ──────────────────────────────────────────────
    private string ResolveNpcName(int index)
    {
        if (_npcNames != null && index < _npcNames.Length && !string.IsNullOrEmpty(_npcNames[index]))
            return _npcNames[index];

        return $"NPC_{index + 1}";
    }

    // ── カラー解決 ────────────────────────────────────────────
    private Color ResolveNpcColor(int index)
    {
        if (_npcColors != null && index < _npcColors.Length)
            return _npcColors[index];

        return Color.HSVToRGB(index / 6f, 0.7f, 0.9f);
    }

    // ── プリミティブ生成ヘルパー ──────────────────────────────
    private static void BuildPrimitive(
        PrimitiveType type,
        Transform     parent,
        Vector3       localPos,
        Vector3       localScale,
        Color         color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        var mat   = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
