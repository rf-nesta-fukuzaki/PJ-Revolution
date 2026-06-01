using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// Sandbox.unity に既に配置されている P プレイヤー(Explorer = ExplorerController)を、
    /// 最初のチャンクコライダーがベイクされた時点で指定 XZ の地表に再配置し "Player" タグを付ける。
    /// L 系 <see cref="SandboxPlayerSpawner"/> の P 版。新規 Instantiate はせず、シーン配置済みの
    /// Explorer を移動・タグ付けするだけ。Director や Checkpoint が GameObject.FindWithTag("Player")
    /// で発見できる状態にする。
    /// </summary>
    public sealed class SandboxExplorerPositioner : MonoBehaviour
    {
        [Tooltip("Sandbox.unity に配置済みの P プレイヤー GameObject 名。")]
        [SerializeField] private string explorerName = "Explorer";

        [Tooltip("プレイヤーを配置するワールド XZ。Y は地形 Raycast で決定。")]
        [SerializeField] private Vector3 spawnXZ = new Vector3(128f, 0f, 128f);

        [Tooltip("地面ヒット点 + これだけ上に配置。Capsule 半径 + 余裕。")]
        [SerializeField] private float spawnHeightOffset = 1.5f;

        [Tooltip("Raycast を始める高度。地形最高点より十分上であること。")]
        [SerializeField] private float raycastFromAltitude = 1000f;

        [Tooltip("コライダーがこの数だけ Ready になるまで待つ。")]
        [SerializeField] private int minReadyChunks = 1;

        [SerializeField] private string playerTag = "Player";

        public bool Positioned { get; private set; }
        public Vector3 SpawnXZ => spawnXZ;

        private SandboxBootstrap _bootstrap;
        private GameObject _explorer;

        private void Awake()
        {
            _bootstrap = GetComponent<SandboxBootstrap>();
            _explorer = GameObject.Find(explorerName);
            if (_explorer == null)
                Debug.LogWarning(
                    $"[SandboxExplorerPositioner] '{explorerName}' が見つかりません。Sandbox.unity に Explorer を配置してください。",
                    this);
        }

        private void Update()
        {
            if (Positioned) return;
            if (_explorer == null) return;
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            if (_bootstrap.ColliderBaker.LastReadyCount < minReadyChunks) return;
            if (!TryFindGround(out var groundPos)) return;

            _explorer.transform.position = groundPos + Vector3.up * spawnHeightOffset;
            ApplyPlayerTag();

            Positioned = true;
            Debug.Log($"[SandboxExplorerPositioner] Explorer placed at {_explorer.transform.position}, tag='{_explorer.tag}'.");
        }

        private bool TryFindGround(out Vector3 pos)
        {
            var origin = new Vector3(spawnXZ.x, raycastFromAltitude, spawnXZ.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, raycastFromAltitude * 2f))
            {
                pos = hit.point;
                return true;
            }
            pos = default;
            return false;
        }

        private void ApplyPlayerTag()
        {
            if (string.IsNullOrEmpty(playerTag)) return;
            if (_explorer.CompareTag(playerTag)) return;
            try { _explorer.tag = playerTag; }
            catch (UnityException)
            {
                Debug.LogWarning(
                    $"[SandboxExplorerPositioner] tag '{playerTag}' は未定義のため設定できません。Tags & Layers に追加してください。",
                    this);
            }
        }
    }
}
