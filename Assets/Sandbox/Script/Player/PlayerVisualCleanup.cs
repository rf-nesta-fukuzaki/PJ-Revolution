using UnityEngine;

/// <summary>
/// PlayerPrefab ルートに残るデバッグ用 Capsule Mesh（黒カプセル）を除去する。
/// ExplorerModel が存在する場合のみ実行。Collider は維持する。
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class PlayerVisualCleanup : MonoBehaviour
{
    private void Awake()
    {
        if (transform.Find("ExplorerModel") == null)
            return;

        if (TryGetComponent(out MeshRenderer renderer))
        {
            renderer.enabled = false;
            Destroy(renderer);
        }

        if (TryGetComponent(out MeshFilter filter))
            Destroy(filter);
    }
}
