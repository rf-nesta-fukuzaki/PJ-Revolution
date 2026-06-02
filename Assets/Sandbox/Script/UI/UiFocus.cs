using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sandbox.UI
{
    /// <summary>
    /// ゲームパッド / キーボードで UI が「どこも選択されていない」状態に陥らないようにする
    /// フォーカスヘルパー。パネルを開いた瞬間に最初の操作可能 Selectable を選択させる。
    ///
    /// マウス前提の自動ナビゲーションだけだと、コントローラーでメニューを開いても
    /// ハイライトが無く操作がスタックする（4人マルチでは特に致命的）。
    /// EventSystem 経由でメタデータ的に検証できる決定的な処理のみ。
    /// </summary>
    public static class UiFocus
    {
        /// <summary>
        /// 明示指定の Selectable を選択する。指定が無効（null/非アクティブ/非操作可）な場合は
        /// fallbackRoot 配下の最初の操作可能 Selectable を探索して選択する。
        /// </summary>
        public static void Select(Selectable preferred, GameObject fallbackRoot = null)
        {
            Selectable target = IsUsable(preferred) ? preferred : FindFirst(fallbackRoot);
            ApplySelection(target);
        }

        /// <summary>root 配下の最初の操作可能 Selectable を選択する。</summary>
        public static void SelectFirst(GameObject root)
        {
            ApplySelection(FindFirst(root));
        }

        private static Selectable FindFirst(GameObject root)
        {
            if (root == null) return null;

            var candidates = root.GetComponentsInChildren<Selectable>(false);
            foreach (var s in candidates)
            {
                if (IsUsable(s)) return s;
            }
            return null;
        }

        private static bool IsUsable(Selectable s)
        {
            return s != null
                   && s.isActiveAndEnabled
                   && s.interactable
                   && s.gameObject.activeInHierarchy;
        }

        private static void ApplySelection(Selectable target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || target == null) return;

            // 旧選択をクリアしてから設定し、確実に SelectionChanged を発火させる。
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(target.gameObject);
        }
    }
}
