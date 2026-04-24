using System;
using UnityEngine;

namespace PeakPlunder.Game
{
    /// <summary>
    /// GDD §17.1 — ゲーム全体ステートマシン。
    ///
    /// 遷移:
    ///   Boot → Splash → TitleScreen → MainMenu
    ///                                 ├→ Lobby → Loading → Basecamp ⇄ Expedition → Returning → Result → MainMenu
    ///                                 ├→ Cosmetic → (back) MainMenu
    ///                                 └→ Settings → (back) MainMenu
    ///
    /// 各ステートは本クラスでは "通知" の責務のみを持つ。
    /// 入場処理 / 退場処理 (GDD §17.2) は各ステート担当コンポーネントが OnStateChanged を購読して実装する。
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        public GameState Current { get; private set; } = GameState.Boot;
        public GameState Previous { get; private set; } = GameState.Boot;

        public event Action<GameState, GameState> OnStateChanged;   // (prev, next)

        [SerializeField] private bool _persistAcrossScenes = true;
        [SerializeField] private bool _logTransitions      = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 指定ステートへ遷移する。許可された遷移のみ受け付ける (GDD §17.1)。
        /// </summary>
        public bool TransitionTo(GameState next)
        {
            if (next == Current) return false;

            if (!IsValidTransition(Current, next))
            {
                Debug.LogWarning($"[GameStateManager] 不正な遷移: {Current} → {next} は許可されていません。");
                return false;
            }

            Previous = Current;
            Current = next;

            if (_logTransitions)
                Debug.Log($"[GameStateManager] {Previous} → {Current}");

            OnStateChanged?.Invoke(Previous, Current);
            return true;
        }

        /// <summary>
        /// 強制遷移（GM ツール・ネットワーク同期など、通常フロー外で必要な場合のみ）。
        /// </summary>
        public void ForceTransition(GameState next)
        {
            if (next == Current) return;
            Previous = Current;
            Current = next;

            if (_logTransitions)
                Debug.Log($"[GameStateManager] FORCED {Previous} → {Current}");

            OnStateChanged?.Invoke(Previous, Current);
        }

        private static bool IsValidTransition(GameState from, GameState to)
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

    public enum GameState
    {
        Boot,
        Splash,
        TitleScreen,
        MainMenu,
        Cosmetic,
        Settings,
        Lobby,
        Loading,
        Basecamp,
        Expedition,
        Returning,
        Result,
        Exit
    }
}
