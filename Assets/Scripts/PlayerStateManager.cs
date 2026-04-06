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

    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _rb.isKinematic = false;
    }

    public void SetState(PlayerState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;
        OnStateChanged(prev, newState);
    }

    public void RequestStateChange(PlayerState newState)
    {
        SetState(newState);
    }

    public void ApplyStun(float duration)
    {
        StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        SetState(PlayerState.Interacting);
        yield return new WaitForSeconds(duration);
        if (CurrentState == PlayerState.Interacting)
            SetState(PlayerState.Normal);
    }

    private void OnStateChanged(PlayerState prev, PlayerState current)
    {
        switch (current)
        {
            case PlayerState.Normal:
            case PlayerState.Falling:
                _rb.isKinematic = false;
                if (prev == PlayerState.Climbing)
                    _rb.AddForce(Vector3.up * antiEmbedForce, ForceMode.VelocityChange);
                break;

            case PlayerState.Climbing:
                _rb.linearVelocity = Vector3.zero;
                _rb.isKinematic = true;
                break;

            case PlayerState.Swinging:
                _rb.isKinematic = false;
                break;

            case PlayerState.Downed:
                _rb.linearVelocity = Vector3.zero;
                break;
        }

        Debug.Log($"[PlayerStateManager] {prev} → {current}");
    }
}

/// <summary>プレイヤーの行動状態。</summary>
public enum PlayerState
{
    Normal,
    Swinging,
    Climbing,
    Falling,
    Downed,
    Interacting,
}
