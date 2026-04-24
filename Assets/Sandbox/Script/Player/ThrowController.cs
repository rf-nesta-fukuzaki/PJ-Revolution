using UnityEngine;

/// <summary>
/// GDD §4.5 — 投擲メカニクス。
///
/// 右クリック（RT/R2）ホールドでパワーゲージ蓄積、リリースで投擲する。
///   - ゲージ充填時間: 2 秒で 0→100%
///   - 投擲力: force = baseForce(5N) + gaugePct × maxForce(20N)
///   - 投擲方向: カメラ正面方向
///   - 投擲対象: <see cref="PlayerInventory"/> の先頭から最初に見つかった非破損アイテム
///
/// ゲージは <see cref="ChargePct"/> で外部から参照可能（HUD クロスヘア周囲の円弧用）。
/// 軌道プレビューは <see cref="GetTrajectoryPoints"/> で 3 点の放物線座標を返す。
/// 死亡中・インベントリ空・カメラ未解決時はチャージ開始しない。
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class ThrowController : MonoBehaviour
{
    private const float CHARGE_DURATION_SECONDS = 2f;  // GDD §4.5 0→100% 充填時間
    private const float BASE_FORCE_NEWTONS      = 5f;  // GDD §4.5 baseForce
    private const float MAX_FORCE_NEWTONS       = 20f; // GDD §4.5 maxForce

    [Header("参照（未設定時は自動解決）")]
    [SerializeField] private Transform _cameraTransform;

    [Header("軌道プレビュー")]
    [Tooltip("投擲物の質量想定（m=1kg の軽量アイテム仮定で軌道を計算）")]
    [SerializeField] private float _simulatedMass = 1f;
    [Tooltip("軌道プレビューのサンプリング間隔（秒）")]
    [SerializeField] private float _trajectoryStepSeconds = 0.2f;

    // ── 依存コンポーネント ────────────────────────────────────
    private PlayerInventory    _inventory;
    private PlayerHealthSystem _health;

    // ── 状態 ────────────────────────────────────────────────
    private float _chargePct;
    private bool  _isCharging;

    /// <summary>0〜1 の充填率。HUD クロスヘア周辺の円弧表示用。</summary>
    public float ChargePct   => _chargePct;
    /// <summary>現在右クリックホールド中でゲージ充填が進行中か。</summary>
    public bool  IsCharging  => _isCharging;
    /// <summary>現在の充填率で投擲した場合の想定初速（m/s）。軌道プレビューに使用。</summary>
    public float CurrentForce => BASE_FORCE_NEWTONS + _chargePct * MAX_FORCE_NEWTONS;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _health    = GetComponent<PlayerHealthSystem>();
        if (_cameraTransform == null)
            _cameraTransform = GetComponentInChildren<Camera>()?.transform ?? transform;
    }

    private void Update()
    {
        // 死亡中は無効。リリース中ゲージが残っていればリセットする。
        if (_health != null && _health.IsDead)
        {
            ResetCharge();
            return;
        }

        bool held = InputStateReader.IsSecondaryPointerHeld();

        if (held)
        {
            if (!_isCharging && FindFirstThrowable() == null) return; // 投げられるアイテムがない
            _isCharging = true;
            _chargePct  = Mathf.Clamp01(_chargePct + Time.deltaTime / CHARGE_DURATION_SECONDS);
        }
        else if (_isCharging)
        {
            ReleaseThrow();
        }
    }

    // ── 公開 API ─────────────────────────────────────────────
    /// <summary>
    /// 軌道プレビュー用に現在チャージで投げたと仮定した位置サンプル（最大 count 点）を返す。
    /// HUD の点線放物線ガイドで使用する（GDD §4.5「3 点予測」）。
    /// </summary>
    public Vector3[] GetTrajectoryPoints(int count = 3)
    {
        if (count <= 0 || _cameraTransform == null) return System.Array.Empty<Vector3>();

        Vector3 origin   = _cameraTransform.position + _cameraTransform.forward * 0.5f;
        Vector3 velocity = _cameraTransform.forward * (CurrentForce / Mathf.Max(0.01f, _simulatedMass));
        Vector3 gravity  = Physics.gravity;

        var points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1) * _trajectoryStepSeconds;
            points[i] = origin + velocity * t + 0.5f * gravity * t * t;
        }
        return points;
    }

    // ── 内部処理 ─────────────────────────────────────────────
    private void ReleaseThrow()
    {
        var item = FindFirstThrowable();
        if (item != null && _cameraTransform != null)
        {
            float force = CurrentForce;
            _inventory.ThrowItem(item, _cameraTransform.forward, force);
            Debug.Log($"[Throw] {item.ItemName} を投擲 pct={_chargePct:F2} force={force:F1}N");
        }
        ResetCharge();
    }

    private void ResetCharge()
    {
        _chargePct  = 0f;
        _isCharging = false;
    }

    private ItemBase FindFirstThrowable()
    {
        var items = _inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && !it.IsBroken) return it;
        }
        return null;
    }
}
