using UnityEngine;
using UnityEngine.InputSystem;
using PeakPlunder.Audio;

/// <summary>
/// PEAK 流のスクランブル登攀。GrabPoint を必要とせず、急斜面・壁に正対して
/// 前進入力を押し込むと直接よじ登れる（スタミナ消費）。頂上に達すると自動で乗り越える。
/// スタミナ切れで手が離れて落下する。点掴み式 <see cref="ClimbingController"/> とは別系統で、
/// そちらが掴み中のときは譲る。登攀中は <see cref="ExplorerController"/> を無効化して操作を奪う
/// （RagdollSystem と同じ流儀）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ScrambleClimbController : MonoBehaviour
{
    [Header("検出")]
    [Tooltip("壁を掴める前方距離 (m)")]
    [SerializeField] private float _reach = 0.95f;
    [Tooltip("レイ原点の高さ（胸の位置）")]
    [SerializeField] private float _chestHeight = 1.1f;
    [Tooltip("この角度より急な面を登攀対象にする (度)")]
    [SerializeField, Range(30f, 89f)] private float _minClimbAngle = 48f;
    [SerializeField] private LayerMask _climbMask;

    [Header("登攀")]
    [SerializeField] private float _climbSpeed   = 2.8f;
    [SerializeField] private float _lateralSpeed = 1.8f;
    [Tooltip("頂上に達したときの乗り越え上向き初速")]
    [SerializeField] private float _mantleUpBoost = 5.2f;
    [Tooltip("頂上に達したときの乗り越え前向き初速")]
    [SerializeField] private float _mantleForwardBoost = 2.8f;
    [Tooltip("壁から飛び降りる初速")]
    [SerializeField] private float _jumpOffBoost = 4.5f;

    private Rigidbody _rb;
    private StaminaSystem _stamina;
    private ExplorerController _controller;
    private ClimbingController _grabClimb;
    private int _inputSlot;

    private bool _isClimbing;
    private Vector3 _wallNormal;

    public bool IsClimbing => _isClimbing;

    private void Awake()
    {
        _rb         = GetComponent<Rigidbody>();
        _stamina    = GetComponent<StaminaSystem>();
        _controller = GetComponent<ExplorerController>();
        _grabClimb  = GetComponent<ClimbingController>();
        _inputSlot    = LocalCoopPartyMember.ResolveInputSlot(this);
        ResolveClimbMask();
    }

    private void Update()
    {
        // 入力スロットは毎フレーム再解決（Awake 時点は未構成で -1 になり得る）。
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
        if (_inputSlot < 0) return;
        if (_isClimbing) UpdateClimbing();
        else TryStartClimbing();
    }

    private void FixedUpdate()
    {
        if (!_isClimbing) return;
        ApplyClimbMotion();
    }

    // ── 開始判定 ─────────────────────────────────────────────
    private void TryStartClimbing()
    {
        // 点掴み式が掴み中、またはスタミナ切れなら登攀しない
        if (_grabClimb != null && _grabClimb.IsClimbing) return;
        if (_stamina != null && _stamina.IsExhausted) return;

        Vector2 move = InputStateReader.ReadMoveVectorRaw(_inputSlot);
        if (move.y <= 0.1f) return; // 前進入力で壁を押し込む

        if (!DetectWall(out Vector3 normal)) return;

        _isClimbing = true;
        _wallNormal = normal;
        if (_controller != null) _controller.enabled = false;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        GameServices.Audio?.PlaySE(SoundId.ClimbGrab, _rb.position);
    }

    // ── 継続処理 ─────────────────────────────────────────────
    private void UpdateClimbing()
    {
        // スタミナ消費し、切れたら手を離す
        _stamina?.ConsumeClimbing();
        if (_stamina != null && _stamina.IsEmpty)
        {
            StopClimbing(Vector3.zero);
            GameServices.Audio?.PlaySE(SoundId.SlideFall, _rb.position);
            return;
        }

        // 壁から飛び降りる
        if (InputStateReader.JumpPressedThisFrame(_inputSlot))
        {
            Vector3 off = _wallNormal * _jumpOffBoost + Vector3.up * _jumpOffBoost * 0.6f;
            StopClimbing(off);
            return;
        }

        // 壁を見失った（頂上 or 端）→ 乗り越え or 終了
        if (!DetectWall(out Vector3 normal))
        {
            Vector2 m = InputStateReader.ReadMoveVectorRaw(_inputSlot);
            if (m.y > 0.1f && HasLedgeAbove())
            {
                Vector3 mantle = -_wallNormal * _mantleForwardBoost + Vector3.up * _mantleUpBoost;
                StopClimbing(mantle);
            }
            else
            {
                StopClimbing(Vector3.zero);
            }
            return;
        }

        _wallNormal = normal;
    }

    private void ApplyClimbMotion()
    {
        Vector2 move = InputStateReader.ReadMoveVectorRaw(_inputSlot);

        // 壁平面に沿った上下・左右ベクトルを構成
        Vector3 wallRight = Vector3.Cross(Vector3.up, _wallNormal).normalized;
        Vector3 wallUp    = Vector3.Cross(_wallNormal, wallRight).normalized;

        Vector3 velocity = wallUp * (move.y * _climbSpeed)
                         + wallRight * (move.x * _lateralSpeed)
                         - _wallNormal * 1.5f; // 壁へ押し付けて密着を保つ
        _rb.linearVelocity = velocity;

        // 壁へ正対
        Vector3 face = -_wallNormal;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(face), 12f * Time.fixedDeltaTime);
    }

    private void StopClimbing(Vector3 launchVelocity)
    {
        _isClimbing = false;
        _rb.useGravity = true;
        if (launchVelocity != Vector3.zero) _rb.linearVelocity = launchVelocity;
        if (_controller != null) _controller.enabled = true;
        GameServices.Audio?.PlaySE(SoundId.ClimbRelease, _rb.position);
    }

    // ── レイキャスト判定 ─────────────────────────────────────
    private bool DetectWall(out Vector3 normal)
    {
        normal = Vector3.zero;
        Vector3 origin = _rb.position + Vector3.up * _chestHeight;
        if (!Physics.Raycast(origin, transform.forward, out RaycastHit hit, _reach, _climbMask, QueryTriggerInteraction.Ignore))
            return false;

        float angle = Vector3.Angle(hit.normal, Vector3.up);
        if (angle < _minClimbAngle) return false; // 歩ける斜面は対象外

        normal = hit.normal;
        return true;
    }

    /// <summary>頂上付近で、前方の足場（乗り越え先）が存在するか判定する。</summary>
    private bool HasLedgeAbove()
    {
        Vector3 origin = _rb.position + Vector3.up * (_chestHeight + 0.8f) - _wallNormal * 0.5f;
        return Physics.Raycast(origin, Vector3.down, 1.2f, _climbMask, QueryTriggerInteraction.Ignore);
    }

    private void ResolveClimbMask()
    {
        int mask = _climbMask.value;
        if (mask == 0) mask = Physics.DefaultRaycastLayers;
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) mask |= 1 << ground;
        int def = LayerMask.NameToLayer("Default");
        if (def >= 0) mask |= 1 << def;
        int player = LayerMask.NameToLayer("Player");
        if (player >= 0) mask &= ~(1 << player);
        mask &= ~(1 << 2); // Ignore Raycast
        _climbMask = mask;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isClimbing ? Color.green : Color.cyan;
        Vector3 origin = transform.position + Vector3.up * _chestHeight;
        Gizmos.DrawLine(origin, origin + transform.forward * _reach);
    }
}
