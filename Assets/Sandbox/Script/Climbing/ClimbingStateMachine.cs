using System;

/// <summary>
/// 登攀状態を管理する純粋 C# ステートマシン（GDD §3.1）。
/// </summary>
public sealed class ClimbingStateMachine
{
    public ClimbingState Current { get; private set; } = ClimbingState.Idle;

    public event Action<ClimbingState, ClimbingState> OnStateChanged;

    public bool IsClimbing => Current == ClimbingState.Climbing;

    public bool TryGrab()
    {
        return TryTransition(ClimbingState.Climbing);
    }

    public bool TryRelease()
    {
        return TryTransition(ClimbingState.Idle);
    }

    public bool TryTransition(ClimbingState next)
    {
        if (Current == next) return false;

        if (!IsValidTransition(Current, next))
        {
            Contract.TryRequires(false,
                $"ClimbingStateMachine: 不正な遷移 {Current} → {next}");
            return false;
        }

        var previous = Current;
        Current = next;
        OnStateChanged?.Invoke(previous, next);
        return true;
    }

    public static bool IsValidTransition(ClimbingState from, ClimbingState to) =>
        (from, to) switch
        {
            (ClimbingState.Idle,     ClimbingState.Climbing) => true,
            (ClimbingState.Climbing, ClimbingState.Idle)     => true,
            _ => false
        };
}

public enum ClimbingState
{
    Idle,
    Climbing
}
