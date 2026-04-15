using UnityEngine;

/// <summary>
/// GDD §7.1 — セミランダムスポーンポイント。
/// L3（遺物）/ L4（天候）/ L5（ハザード）の各レイヤーで使用。
/// ゲーム開始時に SpawnManager がランダムアクティベートする。
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("スポーン設定")]
    [SerializeField] private SpawnLayer _layer;
    [SerializeField] private int        _zoneId;              // ゾーン番号 (1-6)
    [SerializeField] private float      _activateChance = 0.5f;  // アクティベート確率

    [Header("スポーン対象")]
    [SerializeField] private GameObject[] _spawnPrefabs;         // プールから選択
    [SerializeField] private bool         _pickRandom  = true;   // trueでプールからランダム選択

    private GameObject _spawnedObject;
    public bool        IsActive       => _spawnedObject != null;
    public SpawnLayer  Layer          => _layer;
    public int         ZoneId         => _zoneId;

    // ── スポーン ─────────────────────────────────────────────
    /// <summary>確率判定してオブジェクトをスポーンする。</summary>
    public bool TryActivate()
    {
        if (IsActive) return false;
        if (Random.value > _activateChance) return false;

        Activate();
        return true;
    }

    /// <summary>強制的にスポーンする。</summary>
    public void Activate(int prefabIndex = -1)
    {
        if (_spawnPrefabs == null || _spawnPrefabs.Length == 0) return;

        int idx = (_pickRandom || prefabIndex < 0)
            ? Random.Range(0, _spawnPrefabs.Length)
            : Mathf.Clamp(prefabIndex, 0, _spawnPrefabs.Length - 1);

        var prefab = _spawnPrefabs[idx];
        if (prefab == null) return;

        _spawnedObject = Instantiate(prefab, transform.position, transform.rotation);
        // NetworkObject を持つ Prefab は NGO が未起動の場合 SetParent 禁止
        if (_spawnedObject.GetComponent<Unity.Netcode.NetworkObject>() == null)
            _spawnedObject.transform.SetParent(transform);
    }

    /// <summary>スポーンしたオブジェクトを取り除く。</summary>
    public void Deactivate()
    {
        if (_spawnedObject == null) return;
        Destroy(_spawnedObject);
        _spawnedObject = null;
    }

    public GameObject GetSpawnedObject() => _spawnedObject;

    /// <summary>
    /// SpawnManager がランタイムで遺物プールを注入するための API。
    /// Inspector 設定より優先される（_spawnPrefabs を上書き）。
    /// </summary>
    public void SetPrefabPool(GameObject[] pool)
    {
        if (pool == null || pool.Length == 0) return;
        _spawnPrefabs = pool;
        _pickRandom   = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _layer switch
        {
            SpawnLayer.Relic   => Color.yellow,
            SpawnLayer.Hazard  => Color.red,
            SpawnLayer.Route   => Color.green,
            _                  => Color.white
        };
        Gizmos.DrawSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.up * 1f);
    }
}

public enum SpawnLayer { Relic, Hazard, Route, Item }
