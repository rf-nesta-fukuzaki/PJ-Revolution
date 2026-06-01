using System;

/// <summary>
/// 遠征フェーズ遷移を宣言的に管理する純粋 C# ステートマシン。
/// </summary>
public sealed class ExpeditionPhaseStateMachine
{
    public ExpeditionPhase Current { get; private set; } = ExpeditionPhase.Basecamp;

    public event Action<ExpeditionPhase, ExpeditionPhase> OnPhaseChanged;

    public bool TryTransition(ExpeditionPhase next)
    {
        if (Current == next) return false;

        if (!IsValidTransition(Current, next))
        {
            Contract.TryRequires(false,
                $"ExpeditionPhaseStateMachine: 不正な遷移 {Current} → {next}");
            return false;
        }

        var previous = Current;
        Current = next;
        OnPhaseChanged?.Invoke(previous, next);
        return true;
    }

    public static bool IsValidTransition(ExpeditionPhase from, ExpeditionPhase to) =>
        (from, to) switch
        {
            (ExpeditionPhase.Basecamp,  ExpeditionPhase.Climbing)  => true,
            (ExpeditionPhase.Climbing,  ExpeditionPhase.Returning) => true,
            (ExpeditionPhase.Returning, ExpeditionPhase.Result)    => true,
            _ => false
        };
}
