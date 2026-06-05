using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Sandbox.UI; // HudManager, TimerDisplay, IrisTransition

namespace Sandbox.World.Integration
{
    /// <summary>
    /// 既存の単機プレイ・ゲームループ（TimerDisplay / HudManager / AudioManager / IrisTransition）を
    /// Sandbox シーンに生成・接続する接着剤。これらは元々 Mountain01 用シングルトンで Sandbox には未配置のため、
    /// プレイヤーリグが生成・昇格された後に「無ければ生成」する（既存コードは一切変更しない＝非破壊）。
    ///
    /// 生成順の都合:
    ///  - HudManager.Start() は Player タグから GrappleHook/RopeSystem を取得するので、リグ昇格後に生成する必要がある。
    ///  - そのためプレイヤーに RopeSystem が付くまで待ってから一括生成する。
    /// CheckpointSystem は SandboxCheckpointBaker が自前生成するためここでは扱わない。
    /// </summary>
    public sealed class SandboxGameplayDirector : MonoBehaviour
    {
        [SerializeField] private bool ensureFade = true;
        [SerializeField] private bool ensureTimer = true;
        [SerializeField] private bool ensureHud = true;
        [SerializeField] private bool ensureEventSystem = true;
        [SerializeField] private bool addCameraShake = true;
        [SerializeField] private float summitShakeTrauma = 0.6f;

        private SandboxExplorerPositioner _positioner;
        private bool _wired;
        private SandboxCameraShake _shake;
        private bool _summitShaken;

        private void Awake() { _positioner = GetComponent<SandboxExplorerPositioner>(); }

        private void Update()
        {
            if (!_wired)
            {
                TryWire();
                return;
            }
            WatchSummitCelebration();
        }

        private void TryWire()
        {
            // P プレイヤー(Explorer) の位置決め完了を待つ。L 系の RopeSystem 取付ゲートは廃止
            // （Explorer は ExplorerController + ExplorerCameraLook のみ。Rope/HUD は P 側で別途配線）。
            if (_positioner == null || !_positioner.Positioned) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            if (ensureFade) { var _ = GameServices.SceneFade; }
            // Audio: PeakPlunder.Audio.AudioManager は SoundLibrary/AudioMixer の Inspector 配線が必要なため
            //   ここでは自動生成しない。シーンに配置する場合は別途プレハブを用意する。
            //   L 系の AudioManager(プロシージャル波形) は退役済み。
            if (ensureTimer)
            {
                var timer = GameServices.Timer;
                if (timer == null)
                    new GameObject("TimerDisplay").AddComponent<TimerDisplay>();
                else
                    timer.Restart();
            }
            if (ensureHud && Object.FindFirstObjectByType<HudManager>() == null)
                new GameObject("HudManager").AddComponent<HudManager>();
            if (Object.FindFirstObjectByType<InventoryHud>() == null)
                new GameObject("InventoryHud").AddComponent<InventoryHud>();
            if (ensureEventSystem) EnsureEventSystem();

            if (addCameraShake)
            {
                var cam = player.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    _shake = cam.GetComponent<SandboxCameraShake>();
                    if (_shake == null) _shake = cam.gameObject.AddComponent<SandboxCameraShake>();
                }
            }
            _wired = true;
            Debug.Log("[SandboxGameplayDirector] gameplay loop wired (Timer/HUD/Audio/Fade/EventSystem + shake). Footsteps: ExplorerController native.");
        }

        // 山頂到達 = HUD の SummitPanel が active 化 → 一度だけ祝祭シェイク。
        private void WatchSummitCelebration()
        {
            if (_summitShaken || _shake == null) return;
            var panel = GameObject.Find("SummitPanel");
            if (panel != null && panel.activeInHierarchy)
            {
                _shake.AddTrauma(summitShakeTrauma);
                _summitShaken = true;
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // 新 Input System 用 UI モジュール
        }
    }
}
