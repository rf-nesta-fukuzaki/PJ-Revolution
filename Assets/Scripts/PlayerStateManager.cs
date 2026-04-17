using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーの行動状態を管理するステートマシン。
/// ステート追加: Swinging, Climbing, Falling
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerStateManager : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private float antiEmbedForce = 0.5f;

    public MovementState CurrentState { get; private set; } = MovementState.Normal;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _rb.isKinematic = false;
    }

    public void SetState(MovementState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;
        OnStateChanged(prev, newState);
    }

    public void RequestStateChange(MovementState newState)
    {
        SetState(newState);
    }

    public void ApplyStun(float duration)
    {
        StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        SetState(MovementState.Interacting);
        yield return new WaitForSeconds(duration);
        if (CurrentState == MovementState.Interacting)
            SetState(MovementState.Normal);
    }

    private void OnStateChanged(MovementState prev, MovementState current)
    {
        switch (current)
        {
            case MovementState.Normal:
            case MovementState.Falling:
                _rb.isKinematic = false;
                if (prev == MovementState.Climbing)
                    _rb.AddForce(Vector3.up * antiEmbedForce, ForceMode.VelocityChange);
                break;

            case MovementState.Climbing:
                _rb.linearVelocity = Vector3.zero;
                _rb.isKinematic = true;
                break;

            case MovementState.Swinging:
                _rb.isKinematic = false;
                break;

            case MovementState.Downed:
                _rb.linearVelocity = Vector3.zero;
                break;
        }

        Debug.Log($"[PlayerStateManager] {prev} → {current}");
    }
}

/// <summary>プレイヤーの移動・行動ステート（Assets/Scripts 側のレガシーステートマシン用）。</summary>
public enum MovementState
{
    Normal,
    Swinging,
    Climbing,
    Falling,
    Downed,
    Interacting,
}
