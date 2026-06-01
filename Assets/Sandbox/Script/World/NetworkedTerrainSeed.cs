using Unity.Netcode;
using UnityEngine;

namespace Sandbox.World
{
    /// <summary>
    /// NGO 用シード同期。worldSeed をサーバ→全クライアントへ配布し TerrainGenerator に適用する。
    /// 地形は worldSeed から決定論的に生成されるため、シードさえ揃えば全クライアントで同一地形になる
    /// （チャンクのストリーミングは各クライアントのカメラ依存でローカルに走る）。
    ///
    /// 前提: 同 GameObject に NetworkObject、シーンに NetworkManager + Transport が必要。
    /// それらの構築（ホスト/参加 UI 含む）はシーンオーサリング側の作業で、本スクリプトは
    /// 「決定論シードの同期」のみを担う（地形パイプラインに閉じた最小の NGO 結合）。
    /// TerrainGenerator は deferBuildToNetworkSeed=ON にしておくと同期シード到着まで生成を待つ。
    /// </summary>
    [DefaultExecutionOrder(-40)] // TerrainGenerator(-50) の後
    public sealed class NetworkedTerrainSeed : NetworkBehaviour
    {
        [SerializeField] private TerrainGenerator terrainGenerator;
        [Tooltip("サーバ(ホスト)起動時に配布するシード。")]
        [SerializeField] private uint serverSeed = 0;
        [Tooltip("ON ならサーバ起動時にシードをランダム化する。")]
        [SerializeField] private bool randomizeOnServer = false;

        private readonly NetworkVariable<uint> _syncedSeed = new NetworkVariable<uint>(
            0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (terrainGenerator == null)
                terrainGenerator = FindFirstObjectByType<TerrainGenerator>();

            _syncedSeed.OnValueChanged += OnSeedChanged;

            if (IsServer)
            {
                uint seed = randomizeOnServer
                    ? unchecked((uint)Random.Range(int.MinValue, int.MaxValue))
                    : serverSeed;
                _syncedSeed.Value = seed; // 全クライアント(ホスト含む)へ伝播
            }

            // 後から接続したクライアントは初期同期値を OnValueChanged 経由で受け取らないため即適用。
            ApplySeed(_syncedSeed.Value);
        }

        public override void OnNetworkDespawn()
        {
            _syncedSeed.OnValueChanged -= OnSeedChanged;
        }

        private void OnSeedChanged(uint previous, uint current) => ApplySeed(current);

        private void ApplySeed(uint seed)
        {
            if (terrainGenerator != null)
                terrainGenerator.ApplyWorldSeed(seed);
        }
    }
}
