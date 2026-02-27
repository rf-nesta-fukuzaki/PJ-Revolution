using UnityEngine;

/// <summary>
/// シングルプレイ用スポーナー。
/// CaveGenerator の洞窟生成完了後、プレイヤーを開始地点に移動させる。
/// スポーン位置はスカラー場を走査して床面を検出し、その上に補正する。
/// </summary>
public class SimpleSpawner : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("シーン上の CaveGenerator")]
    [SerializeField] private CaveGenerator _caveGenerator;

    [Tooltip("移動させるプレイヤーの Transform")]
    [SerializeField] private Transform _playerTransform;

    [Header("設定")]
    [Tooltip("スポーン時にプレイヤーのRigidbodyのvelocityをリセットするか")]
    [SerializeField] private bool _resetVelocity = true;

    [Tooltip("床面検出後、足元からどれだけ上にスポーンするか（m）")]
    [SerializeField] private float _spawnHeightOffset = 1.5f;

    private void OnEnable()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated += HandleCaveGenerated;
    }

    private void OnDisable()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated -= HandleCaveGenerated;
    }

    private void HandleCaveGenerated()
    {
        if (_playerTransform == null)
        {
            Debug.LogError("[SimpleSpawner] playerTransform が未設定です");
            return;
        }

        Vector3 centerPos = _caveGenerator.StartWorldPosition;
        Vector3 spawnPos = FindFloorBelow(centerPos);

        Debug.Log($"[SimpleSpawner] プレイヤーを {spawnPos} にスポーンします（空洞中心: {centerPos}）");

        _playerTransform.position = spawnPos;

        if (_resetVelocity)
        {
            var rb = _playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 指定座標から下方向にスカラー場を調べて床面を検出する。
    /// 床面 = 空洞（scalar &lt; isoLevel）の直下が岩（scalar &gt;= isoLevel）となるY座標。
    /// </summary>
    private Vector3 FindFloorBelow(Vector3 startPos)
    {
        var chunks = _caveGenerator.Chunks;
        if (chunks == null || chunks.Count == 0)
        {
            Debug.LogWarning("[SimpleSpawner] チャンクが空のためフォールバック位置を使用");
            return startPos + Vector3.up * _spawnHeightOffset;
        }

        float isoLevel = _caveGenerator.NoiseConfig.isoLevel;
        float cellSize = _caveGenerator.CellSize3D;

        // startPos から下方向に cellSize 刻みでスキャン
        for (float y = startPos.y; y > 0f; y -= cellSize)
        {
            Vector3 checkPos = new Vector3(startPos.x, y, startPos.z);
            Vector3 belowPos = new Vector3(startPos.x, y - cellSize, startPos.z);

            float scalarHere  = GetScalarAtWorld(checkPos, chunks, cellSize);
            float scalarBelow = GetScalarAtWorld(belowPos, chunks, cellSize);

            bool isAirHere    = scalarHere  < isoLevel;
            bool isSolidBelow = scalarBelow >= isoLevel;

            if (isAirHere && isSolidBelow)
            {
                Vector3 floorPos = new Vector3(startPos.x, y + _spawnHeightOffset, startPos.z);
                Debug.Log($"[SimpleSpawner] 床面検出: Y={y:F1}, スポーン: Y={floorPos.y:F1}");
                return floorPos;
            }
        }

        // フォールバック: Physics.Raycast
        if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, 100f))
        {
            Debug.Log($"[SimpleSpawner] Raycast フォールバック: 床面 Y={hit.point.y:F1}");
            return hit.point + Vector3.up * _spawnHeightOffset;
        }

        // 最終フォールバック
        Debug.LogWarning("[SimpleSpawner] 床面が見つかりません。空洞中心 +3m を使用");
        return startPos + Vector3.up * 3f;
    }

    /// <summary>
    /// ワールド座標からスカラー値を取得する。
    /// 該当チャンクを探してローカル座標に変換する。
    /// </summary>
    private float GetScalarAtWorld(Vector3 worldPos,
        System.Collections.Generic.IReadOnlyList<CaveChunk> chunks, float cellSize)
    {
        int cs = CaveChunk.ChunkSize;

        foreach (var chunk in chunks)
        {
            if (chunk == null) continue;

            Vector3 localPos = worldPos - chunk.transform.position;
            int lx = Mathf.RoundToInt(localPos.x / cellSize);
            int ly = Mathf.RoundToInt(localPos.y / cellSize);
            int lz = Mathf.RoundToInt(localPos.z / cellSize);

            if (lx >= 0 && lx <= cs && ly >= 0 && ly <= cs && lz >= 0 && lz <= cs)
                return chunk.GetScalar(lx, ly, lz);
        }

        return 1f; // 範囲外は岩扱い
    }
}
