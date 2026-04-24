using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §3.1 — ポイント＆グラブ方式の登攀コントローラー。
/// - 壁に近づくと掴めるポイントをハイライト
/// - ボタンで掴む/離す
/// - スタミナ管理
/// ExplorerController と同じ GameObject にアタッチする。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ClimbingController : MonoBehaviour
{
    // IceAxeItem.ItemNameKey を参照して重複宣言を排除（リネーム時の食い違いを防ぐ）。

    [Header("検出設定")]
    [SerializeField] private float _detectionRadius  = 2.5f;
    [SerializeField] private LayerMask _grabLayer;

    [Header("クライミング物理")]
    [SerializeField] private float _pullSpeed        = 4f;     // グラブポイントへの引き寄せ速度
    [SerializeField] private float _holdGravityScale = 0.2f;   // 掴んでいるときの重力スケール
    [SerializeField] private float _releaseImpulse   = 3f;     // 離したときの勢い

    [Header("スタミナ")]
    [SerializeField] private StaminaSystem _stamina;

    private Rigidbody         _rb;
    private PlayerInventory   _inventory;
    private Animator          _animator;
    private GrabPoint         _nearestGrabPoint;
    private GrabPoint         _currentGrabPoint;
    private bool              _isClimbing;

    private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");

    // ハイライト管理
    private readonly HashSet<GrabPoint> _highlighted = new();

    // GC ゼロの Overlap バッファ — Update 毎に new[] しない
    private readonly Collider[]         _overlapBuffer = new Collider[32];
    // スキャン結果を積み上げる再利用セット — new HashSet<> を毎フレーム生成しない
    private readonly HashSet<GrabPoint> _scanResultSet = new();

    // ── プロパティ ────────────────────────────────────────────
    public bool IsClimbing    => _isClimbing;
    public GrabPoint HeldPoint => _currentGrabPoint;

    private void Awake()
    {
        _rb        = GetComponent<Rigidbody>();
        _inventory = GetComponent<PlayerInventory>();
        _animator  = GetComponentInChildren<Animator>();
        if (_stamina == null)
            _stamina = GetComponent<StaminaSystem>();
    }

    private void Update()
    {
        ScanNearbyGrabPoints();
        HandleGrabInput();
    }

    private void FixedUpdate()
    {
        if (!_isClimbing) return;

        ApplyClimbingPhysics();
    }

    // ── スキャン ─────────────────────────────────────────────
    private void ScanNearbyGrabPoints()
    {
        // NonAlloc 版: _overlapBuffer に収まるだけ取得（GC ゼロ）
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, _detectionRadius, _overlapBuffer, _grabLayer);

        // 再利用セットをクリアして今フレームの結果を積む
        _scanResultSet.Clear();
        GrabPoint closest  = null;
        float     closestD = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var gp = _overlapBuffer[i].GetComponent<GrabPoint>();
            if (gp == null || gp.IsOccupied) continue;

            _scanResultSet.Add(gp);
            float d = Vector3.Distance(transform.position, gp.transform.position);
            if (d < closestD)
            {
                closestD = d;
                closest  = gp;
            }
        }

        // 外れたポイントのハイライトを解除
        foreach (var gp in _highlighted)
        {
            if (!_scanResultSet.Contains(gp))
                gp.SetHighlight(false);
        }

        _highlighted.Clear();

        // 新たなポイントをハイライト
        foreach (var gp in _scanResultSet)
        {
            gp.SetHighlight(true);
            _highlighted.Add(gp);
        }

        _nearestGrabPoint = closest;
    }

    // ── 入力 ─────────────────────────────────────────────────
    private void HandleGrabInput()
    {
        if (InputStateReader.InteractPressedThisFrame())
        {
            if (_isClimbing)
                ReleaseGrab();
            else
                TryGrab();
        }
    }

    // ── グラブ ────────────────────────────────────────────────
    private void TryGrab()
    {
        if (_nearestGrabPoint == null) return;

        // GDD §5.2 — 氷壁グリップに必要なアイスアックスを検証し、1 回分の耐久を消費する。
        // 破損（15 回到達）済みなら掴めない。
        if (_nearestGrabPoint.RequireIceAxe && !ConsumeIceAxeUse()) return;

        if (!_nearestGrabPoint.TryOccupy()) return;

        _currentGrabPoint = _nearestGrabPoint;
        _isClimbing       = true;

        // 慣性を消す
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Animator: IsClimbing (GDD §16.2)
        _animator?.SetBool(IsClimbingHash, true);

        // GDD §15.2 — climb_grab
        PPAudioManager.Instance?.PlaySE(SoundId.ClimbGrab, _currentGrabPoint.transform.position);

        Debug.Log($"[Climbing] グラブ: {_currentGrabPoint.name}");
    }

    private void ReleaseGrab()
    {
        if (_currentGrabPoint == null) return;

        Vector3 grabPos = _currentGrabPoint.transform.position;

        _currentGrabPoint.Release();
        _currentGrabPoint = null;
        _isClimbing       = false;

        // Animator: IsClimbing (GDD §16.2)
        _animator?.SetBool(IsClimbingHash, false);

        // GDD §15.2 — climb_release
        PPAudioManager.Instance?.PlaySE(SoundId.ClimbRelease, grabPos);

        // 上方向への勢い付与（プレイヤーが次のポイントへ跳べるように）
        _rb.AddForce(Vector3.up * _releaseImpulse + transform.forward * _releaseImpulse * 0.5f,
                     ForceMode.Impulse);
    }

    // ── 登攀物理 ─────────────────────────────────────────────
    private void ApplyClimbingPhysics()
    {
        if (_currentGrabPoint == null)
        {
            _isClimbing = false;
            _animator?.SetBool(IsClimbingHash, false);
            return;
        }

        // スタミナ消費
        if (_stamina != null)
        {
            _stamina.Consume(_currentGrabPoint.StaminaDrain * Time.fixedDeltaTime);

            // スタミナ切れで落下
            if (_stamina.IsEmpty)
            {
                ReleaseGrab();
                return;
            }
        }

        // グラブポイントへの引き寄せ（グラブポイントの高さに合わせる）
        Vector3 targetPos  = _currentGrabPoint.transform.position
                             - transform.up * 0.8f;   // キャラクターの腕の高さ分オフセット
        Vector3 toTarget   = targetPos - _rb.position;

        if (toTarget.magnitude > 0.1f)
        {
            _rb.linearVelocity = toTarget.normalized * _pullSpeed;
        }
        else
        {
            // ポイントに到達したらその場で保持（低重力）
            Vector3 vel = _rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, -0.5f) * _holdGravityScale;
            _rb.linearVelocity = vel;

            // 追加の反重力力
            _rb.AddForce(-Physics.gravity * (1f - _holdGravityScale), ForceMode.Acceleration);
        }

        // 入力による上下移動
        float vertInput = InputStateReader.ReadVerticalAxisRaw();
        if (Mathf.Abs(vertInput) > 0.1f)
        {
            _rb.AddForce(Vector3.up * vertInput * _pullSpeed * 0.5f, ForceMode.Acceleration);
        }
    }

    // ── ユーティリティ ────────────────────────────────────────
    // GDD §5.2 — 氷壁グラブ時の耐久消費。成功時のみ掴める。
    private bool ConsumeIceAxeUse()
    {
        if (_inventory == null) return false;
        var axe = _inventory.GetItem(IceAxeItem.ItemNameKey) as IceAxeItem;
        return axe != null && axe.TryUse();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
}
