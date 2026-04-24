using UnityEngine;
using PPGameState = PeakPlunder.Game.GameState;
using PPGameStateManager = PeakPlunder.Game.GameStateManager;

namespace PeakPlunder.Audio
{
    /// <summary>
    /// GDD §15.1 / §17.2 — ゲームステート遷移に応じて BGM をクロスフェード切替するブリッジ。
    /// GameStateManager.OnStateChanged を購読し、Inspector でアサインされた AudioClip を
    /// AudioManager.PlayBGM(...) に渡す。AudioClip 未アサイン時は該当遷移で何もしない
    /// （audio designer が後からクリップを差し込めるように、呼び出し側はクラッシュしない）。
    ///
    /// 実装メモ: Assets/Scripts/GameManager.cs に global namespace の別 GameState enum
    /// (Playing/Clear/Paused) が存在するため、名前衝突を避けるため using alias を使用。
    /// </summary>
    public class BgmController : MonoBehaviour
    {
        [Header("ステート別 BGM (GDD §15.1 / §17.2)")]
        [SerializeField] private AudioClip _titleBgm;
        [SerializeField] private AudioClip _mainMenuBgm;
        [SerializeField] private AudioClip _basecampBgm;
        [SerializeField] private AudioClip _expeditionBgm;
        [SerializeField] private AudioClip _returningBgm;
        [SerializeField] private AudioClip _resultBgm;

        [Header("音量 (GDD §15.1)")]
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.5f;

        private PPGameStateManager _gsm;

        private void OnEnable()
        {
            _gsm = PPGameStateManager.Instance;
            if (_gsm != null)
                _gsm.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_gsm != null)
                _gsm.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // Instance が Awake 順序で未解決だった場合の遅延フォロー
            if (_gsm == null)
            {
                _gsm = PPGameStateManager.Instance;
                if (_gsm != null) _gsm.OnStateChanged += HandleStateChanged;
            }
        }

        private void HandleStateChanged(PPGameState prev, PPGameState next)
        {
            AudioClip clip = next switch
            {
                PPGameState.TitleScreen => _titleBgm,
                PPGameState.MainMenu    => _mainMenuBgm,
                PPGameState.Basecamp    => _basecampBgm,
                PPGameState.Expedition  => _expeditionBgm,
                PPGameState.Returning   => _returningBgm,
                PPGameState.Result      => _resultBgm,
                _                       => null,
            };

            if (clip == null) return;
            AudioManager.Instance?.PlayBGM(clip, _bgmVolume);
        }
    }
}
