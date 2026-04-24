using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GDD §14.7 — キーバインド変更 UI のロジック層。
/// Unity Input System の `InputActionRebindingExtensions.PerformInteractiveRebinding` を使い、
/// プレイヤーが押した次のキー/ボタンを新しい割り当てとして適用する。
///
/// 競合検出: 新しい path が同じ ActionMap 内の他の Action で既に使われている場合、
/// `OnConflict` イベントで UI に通知し、UI から Resolve(swap / cancel) を呼ぶ。
///
/// 永続化: 設定変更は `SaveBindingOverridesAsJson` で PlayerPrefs に保存され、起動時に復元される。
/// </summary>
[DisallowMultipleComponent]
public class KeyRebindController : MonoBehaviour
{
    public static KeyRebindController Instance { get; private set; }

    private const string PREFS_KEY = "peakplunder.input.rebinds";

    [Tooltip("対象の InputActionAsset。Inspector でプロジェクトの .inputactions を割り当てる。")]
    [SerializeField] private InputActionAsset _actions;

    // 現在進行中のリバインド操作。競合解決待ちの間は保持する。
    private InputActionRebindingExtensions.RebindingOperation _activeOp;
    private Action<RebindResult> _pendingCallback;

    /// <summary>競合発生時の情報。UI が ResolveConflict(...) で応答する。</summary>
    public sealed class ConflictInfo
    {
        public InputAction TargetAction;
        public int         TargetBindingIndex;
        public InputAction ConflictingAction;
        public int         ConflictingBindingIndex;
        public string      NewPath;
    }

    public enum RebindResult { Applied, Cancelled, Swapped }

    /// <summary>競合発生通知。UI が subscribe して確認ダイアログを出す。</summary>
    public event Action<ConflictInfo> OnConflict;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadOverrides();
    }

    private void OnDestroy()
    {
        CancelActive();
        if (Instance == this) Instance = null;
    }

    // ── Public API ───────────────────────────────────────────
    /// <summary>
    /// 指定 Action の指定 binding インデックスを対話的に再バインドする。
    /// 成功時 callback(Applied or Swapped)、ユーザーキャンセル時 callback(Cancelled)。
    /// </summary>
    public void StartRebind(InputAction action, int bindingIndex, Action<RebindResult> callback)
    {
        if (action == null)
        {
            Debug.LogError("[Contract] KeyRebindController.StartRebind: action が null です");
            callback?.Invoke(RebindResult.Cancelled);
            return;
        }
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            Debug.LogError($"[Contract] KeyRebindController.StartRebind: bindingIndex {bindingIndex} は範囲外です");
            callback?.Invoke(RebindResult.Cancelled);
            return;
        }

        CancelActive();

        bool wasEnabled = action.enabled;
        action.Disable();
        _pendingCallback = callback;

        _activeOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnCancel(op => FinishRebind(action, bindingIndex, wasEnabled, RebindResult.Cancelled))
            .OnComplete(op => OnRebindComplete(action, bindingIndex, wasEnabled))
            .Start();
    }

    /// <summary>ユーザーが競合ダイアログで「入れ替える」を選んだ場合に呼ぶ。</summary>
    public void ResolveConflictBySwap(ConflictInfo info)
    {
        if (info == null) return;

        // 現在の両者の path を取り出し、衝突先を現在自分が持っていた path に書き換える。
        string currentNewActionPath = info.TargetAction.bindings[info.TargetBindingIndex].effectivePath;
        string oldSelfPath = info.TargetAction.bindings[info.TargetBindingIndex].path;

        // 衝突先の override を「元々自分が持っていた」path にする。
        info.ConflictingAction.ApplyBindingOverride(info.ConflictingBindingIndex, oldSelfPath);

        SaveOverrides();
        _pendingCallback?.Invoke(RebindResult.Swapped);
        _pendingCallback = null;
    }

    /// <summary>ユーザーが競合ダイアログで「キャンセル」を選んだ場合に呼ぶ。</summary>
    public void ResolveConflictByCancel(ConflictInfo info)
    {
        if (info == null) return;

        // 新しい割り当てを取り消す。
        info.TargetAction.RemoveBindingOverride(info.TargetBindingIndex);
        SaveOverrides();
        _pendingCallback?.Invoke(RebindResult.Cancelled);
        _pendingCallback = null;
    }

    /// <summary>指定 Action の全バインドをデフォルトに戻す。</summary>
    public void ResetAction(InputAction action)
    {
        if (action == null) return;
        action.RemoveAllBindingOverrides();
        SaveOverrides();
    }

    /// <summary>全 Action のバインドをデフォルトに戻す。</summary>
    public void ResetAll()
    {
        if (_actions == null) return;
        _actions.RemoveAllBindingOverrides();
        SaveOverrides();
    }

    /// <summary>指定 binding の表示文字列（例: "Space", "Left Button"）。UI ラベル用。</summary>
    public string GetDisplayString(InputAction action, int bindingIndex)
    {
        if (action == null) return string.Empty;
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return string.Empty;
        return action.GetBindingDisplayString(bindingIndex);
    }

    // ── 内部処理 ─────────────────────────────────────────────
    private void OnRebindComplete(InputAction action, int bindingIndex, bool wasEnabled)
    {
        string newPath = action.bindings[bindingIndex].effectivePath;

        // 同 ActionMap 内の他 Action で同じ path が使われていないかチェック。
        var conflict = FindConflict(action, bindingIndex, newPath);
        if (conflict != null)
        {
            // UI に通知し、Resolve を待つ（_pendingCallback は保持）。
            DisposeOp();
            if (wasEnabled) action.Enable();
            OnConflict?.Invoke(conflict);
            return;
        }

        SaveOverrides();
        FinishRebind(action, bindingIndex, wasEnabled, RebindResult.Applied);
    }

    private void FinishRebind(InputAction action, int bindingIndex, bool wasEnabled, RebindResult result)
    {
        DisposeOp();
        if (wasEnabled) action.Enable();

        var cb = _pendingCallback;
        _pendingCallback = null;
        cb?.Invoke(result);
    }

    private ConflictInfo FindConflict(InputAction target, int targetBindingIndex, string newPath)
    {
        if (_actions == null || string.IsNullOrEmpty(newPath)) return null;

        var map = target.actionMap;
        if (map == null) return null;

        foreach (var otherAction in map.actions)
        {
            for (int i = 0; i < otherAction.bindings.Count; i++)
            {
                // 自分自身はスキップ
                if (otherAction == target && i == targetBindingIndex) continue;

                var b = otherAction.bindings[i];
                if (b.isComposite) continue; // 複合バインドは各 part でチェック
                if (b.effectivePath == newPath)
                {
                    return new ConflictInfo
                    {
                        TargetAction            = target,
                        TargetBindingIndex      = targetBindingIndex,
                        ConflictingAction       = otherAction,
                        ConflictingBindingIndex = i,
                        NewPath                 = newPath
                    };
                }
            }
        }
        return null;
    }

    // ── 永続化 ───────────────────────────────────────────────
    private void SaveOverrides()
    {
        if (_actions == null) return;
        string json = _actions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadOverrides()
    {
        if (_actions == null) return;
        string json = PlayerPrefs.GetString(PREFS_KEY, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        _actions.LoadBindingOverridesFromJson(json);
    }

    private void CancelActive()
    {
        if (_activeOp == null) return;
        try { _activeOp.Cancel(); } catch { /* swallow double-dispose */ }
        DisposeOp();
    }

    private void DisposeOp()
    {
        if (_activeOp == null) return;
        _activeOp.Dispose();
        _activeOp = null;
    }

    // ── ゲームパッドプリセット（GDD §14.7 代替A/B）────────────
    /// <summary>
    /// ゲームパッドプリセット切替用。対象 ActionMap 内の gamepad binding を一括置換する。
    /// preset: 0=Default / 1=AlternativeA / 2=AlternativeB
    /// </summary>
    public void ApplyGamepadPreset(int preset, string actionMapName = "Player")
    {
        if (_actions == null) return;
        var map = _actions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null) return;

        // 代替プリセットは Action 名を決め打ちで扱う（GDD §14.7 準拠）。
        var rules = GetPresetRules(preset);
        foreach (var kv in rules)
        {
            var act = map.FindAction(kv.Key, throwIfNotFound: false);
            if (act == null) continue;
            // gamepad control scheme の binding にだけ上書きを適用。
            for (int i = 0; i < act.bindings.Count; i++)
            {
                var b = act.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (b.groups != null && b.groups.Contains("Gamepad"))
                    act.ApplyBindingOverride(i, kv.Value);
            }
        }
        SaveOverrides();
    }

    private static Dictionary<string, string> GetPresetRules(int preset)
    {
        // GDD §14.7 「ゲームパッドプリセット定義」表を機械可読形にしたもの。
        // 代表的な Action のみ上書きし、他はデフォルトを尊重する。
        return preset switch
        {
            1 => new Dictionary<string, string>
            {
                { "Jump",    "<Gamepad>/buttonEast"  }, // B/○
                { "Interact","<Gamepad>/buttonSouth" }, // A/×
            },
            2 => new Dictionary<string, string>
            {
                { "Dash",    "<Gamepad>/leftShoulder" }, // LB/L1 ホールド
                { "Cut",     "<Gamepad>/rightShoulder" } // RB/R1
            },
            _ => new Dictionary<string, string>()
        };
    }
}
