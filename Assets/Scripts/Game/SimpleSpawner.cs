using System.Collections;
using UnityEngine;

/// <summary>
/// シングルプレイ用スポーナー。
/// CaveGenerator の洞窟生成完了後、実際のメッシュコライダー上面を検出して
/// プレイヤーを確実に床の上にスポーンさせる。
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

    [Tooltip("メッシュ表面からどれだけ上にスポーンするか（m）")]
    [SerializeField] private float _spawnHeightOffset = 2.0f;

    [Tooltip("Raycast の開始高さオフセット（空洞中心から上にどれだけずらすか）")]
    [SerializeField] private float _raycastStartOffset = 20f;

    [Tooltip("Raycast の最大距離")]
    [SerializeField] private float _raycastMaxDistance = 100f;

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

        // MeshCollider が bake されるのを待つために1フレーム遅延
        StartCoroutine(SpawnAfterFrame());
    }

    private IEnumerator SpawnAfterFrame()
    {
        // Physics の更新を待つ（MeshCollider の bake 完了を保証）
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Vector3 cavityCenter = _caveGenerator.StartWorldPosition;
        Vector3 spawnPos = FindFloorByRaycast(cavityCenter);

        Debug.Log($"[SimpleSpawner] プレイヤーを {spawnPos} にスポーンします（空洞中心: {cavityCenter}）");

        _playerTransform.position = spawnPos;

        if (_resetVelocity)
        {
            var rb = _playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 空洞中心の真上から下に Raycast して、最初にヒットするメッシュコライダー表面を
    /// 床面として検出する。強制空洞の中を通過するため、空洞内の床にヒットする。
    ///
    /// Raycast 起点を空洞中心より十分上（天井の外）に設定し、下向きに撃つ。
    /// 最初のヒット = 天井面。2回目以降のヒットを探して床面を見つける。
    ///
    /// ただし強制空洞は半径8mで天井を削っているため、空洞内から上に向けた Raycast で
    /// 天井に当たらないケースもある。そのため複数の方法を試す。
    /// </summary>
    private Vector3 FindFloorByRaycast(Vector3 cavityCenter)
    {
        // 方法1: 空洞中心から直接下に Raycast
        // （強制空洞内なので、中心付近は空気。下に撃てば床面に当たるはず）
        Vector3 rayOrigin = cavityCenter;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit1, _raycastMaxDistance))
        {
            Vector3 result = hit1.point + Vector3.up * _spawnHeightOffset;
            Debug.Log($"[SimpleSpawner] 方法1成功: Raycast 下向き hit={hit1.point}, spawn={result}");
            return result;
        }

        // 方法2: 空洞中心の上方から下に Raycast（天井を貫通して床を探す）
        // RaycastAll で全ヒットを取得し、空洞中心より下のヒットを床面とする
        Vector3 highOrigin = cavityCenter + Vector3.up * _raycastStartOffset;
        RaycastHit[] hits = Physics.RaycastAll(highOrigin, Vector3.down, _raycastMaxDistance);

        if (hits.Length > 0)
        {
            // Y座標が空洞中心より下で、最も空洞中心に近いヒットを床面とする
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // 空洞中心より下のヒット = 床面（天井面ではない）
                if (hit.point.y < cavityCenter.y)
                {
                    Vector3 result = hit.point + Vector3.up * _spawnHeightOffset;
                    Debug.Log($"[SimpleSpawner] 方法2成功: RaycastAll hit={hit.point}, spawn={result}");
                    return result;
                }
            }

            // 全ヒットが空洞中心より上 = 全て天井。最後のヒットの少し上にスポーン
            var lastHit = hits[hits.Length - 1];
            Vector3 fallback = lastHit.point + Vector3.up * _spawnHeightOffset;
            Debug.Log($"[SimpleSpawner] 方法2フォールバック: 最深ヒット={lastHit.point}, spawn={fallback}");
            return fallback;
        }

        // 方法3: 最終フォールバック（Raycast 全失敗）
        Debug.LogWarning("[SimpleSpawner] 全Raycast失敗。空洞中心をそのまま使用");
        return cavityCenter;
    }
}
