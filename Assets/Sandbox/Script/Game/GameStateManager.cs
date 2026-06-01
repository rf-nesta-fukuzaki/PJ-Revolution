using UnityEngine;

namespace PeakPlunder.Game
{
    /// <summary>
    /// GDD §17.1 — ゲーム全体ステートマシンの MonoBehaviour アダプター。
    /// 遷移ロジックは <see cref="GamePhaseStateMachine"/> に委譲する。
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        private readonly GamePhaseStateMachine _stateMachine = new();

        public GameState Current => _stateMachine.Current;
        public GameState Previous => _stateMachine.Previous;

        public event System.Action<GameState, GameState> OnStateChanged
        {
            add => _stateMachine.OnStateChanged += value;
            remove => _stateMachine.OnStateChanged -= value;
        }

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

        public bool TransitionTo(GameState next)
        {
            if (!_stateMachine.TryTransition(next))
                return false;

            if (_logTransitions)
                UnityEngine.Debug.Log($"[GameStateManager] {Previous} → {Current}");

            return true;
        }

        public void ForceTransition(GameState next)
        {
            _stateMachine.ForceTransition(next);

            if (_logTransitions)
                UnityEngine.Debug.Log($"[GameStateManager] FORCED {Previous} → {Current}");
        }
    }
}
