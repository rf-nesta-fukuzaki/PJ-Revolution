using System;
using PeakPlunder.Game;

namespace PeakPlunder.Game
{
    /// <summary>
    /// ゲーム全体フェーズ遷移の純粋 C# ステートマシン（GDD §17.1）。
    /// </summary>
    public sealed class GamePhaseStateMachine
    {
        public GameState Current { get; private set; } = GameState.Boot;
        public GameState Previous { get; private set; } = GameState.Boot;

        public event Action<GameState, GameState> OnStateChanged;

        public bool TryTransition(GameState next)
        {
            if (next == Current) return false;

            if (!IsValidTransition(Current, next))
            {
                Contract.TryRequires(false,
                    $"GamePhaseStateMachine: 不正な遷移 {Current} → {next}");
                return false;
            }

            Previous = Current;
            Current = next;
            OnStateChanged?.Invoke(Previous, Current);
            return true;
        }

        public void ForceTransition(GameState next)
        {
            if (next == Current) return;
            Previous = Current;
            Current = next;
            OnStateChanged?.Invoke(Previous, Current);
        }

        public static bool IsValidTransition(GameState from, GameState to)
        {
            switch (from)
            {
                case GameState.Boot:        return to == GameState.Splash;
                case GameState.Splash:      return to == GameState.TitleScreen;
                case GameState.TitleScreen: return to == GameState.MainMenu;
                case GameState.MainMenu:    return to is GameState.Lobby
                                                    or GameState.Cosmetic
                                                    or GameState.Settings
                                                    or GameState.Exit;
                case GameState.Cosmetic:    return to == GameState.MainMenu;
                case GameState.Settings:    return to == GameState.MainMenu;
                case GameState.Lobby:       return to is GameState.Loading or GameState.MainMenu;
                case GameState.Loading:     return to == GameState.Basecamp;
                case GameState.Basecamp:    return to is GameState.Expedition or GameState.MainMenu;
                case GameState.Expedition:  return to is GameState.Returning or GameState.MainMenu;
                case GameState.Returning:   return to == GameState.Result;
                case GameState.Result:      return to == GameState.MainMenu;
                default:                    return false;
            }
        }
    }
}
