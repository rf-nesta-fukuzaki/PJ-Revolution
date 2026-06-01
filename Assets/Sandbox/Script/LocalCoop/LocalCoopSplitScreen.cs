using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ローカル Co-op 人間プレイヤーごとにカメラビューポートを分割する。
/// </summary>
public sealed class LocalCoopSplitScreen : MonoBehaviour
{
    private static readonly Rect[] SingleLayout = { new Rect(0f, 0f, 1f, 1f) };

    private static readonly Rect[] DualLayout =
    {
        new Rect(0f, 0f, 0.5f, 1f),
        new Rect(0.5f, 0f, 0.5f, 1f),
    };

    private static readonly Rect[] TripleLayout =
    {
        new Rect(0f, 0f, 0.5f, 1f),
        new Rect(0.5f, 0.5f, 0.5f, 0.5f),
        new Rect(0.5f, 0f, 0.5f, 0.5f),
    };

    private static readonly Rect[] QuadLayout =
    {
        new Rect(0f, 0.5f, 0.5f, 0.5f),
        new Rect(0.5f, 0.5f, 0.5f, 0.5f),
        new Rect(0f, 0f, 0.5f, 0.5f),
        new Rect(0.5f, 0f, 0.5f, 0.5f),
    };

    private readonly List<Camera> _humanCameras = new();
    private bool _applied;

    public void ApplyLayout(IReadOnlyList<ExplorerController> humanPlayers)
    {
        _humanCameras.Clear();
        if (humanPlayers == null || humanPlayers.Count == 0) return;

        foreach (var explorer in humanPlayers)
        {
            if (explorer == null) continue;
            var cam = explorer.GetComponentInChildren<Camera>();
            if (cam == null) continue;
            cam.enabled = true;
            cam.depth = 0;
            _humanCameras.Add(cam);
        }

        Rect[] layout = ResolveLayout(_humanCameras.Count);
        for (int i = 0; i < _humanCameras.Count; i++)
        {
            var cam = _humanCameras[i];
            cam.rect = layout[Mathf.Min(i, layout.Length - 1)];
            cam.tag = i == 0 ? "MainCamera" : "Untagged";

            var listener = cam.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = i == 0;

            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null)
                urp.renderPostProcessing = true;
        }

        DisableSceneMainCameraExcept(_humanCameras);
        _applied = true;
        Debug.Log($"[LocalCoop] スプリット画面: {_humanCameras.Count} ビュー");
    }

    private static Rect[] ResolveLayout(int count) => count switch
    {
        1 => SingleLayout,
        2 => DualLayout,
        3 => TripleLayout,
        _ => QuadLayout,
    };

    private static void DisableSceneMainCameraExcept(List<Camera> keep)
    {
        var all = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in all)
        {
            if (cam == null || keep.Contains(cam)) continue;
            if (!cam.CompareTag("MainCamera")) continue;
            cam.enabled = false;
            cam.tag = "Untagged";
        }
    }

    public Camera PrimaryCamera => _humanCameras.Count > 0 ? _humanCameras[0] : null;
    public bool Applied => _applied;
}
