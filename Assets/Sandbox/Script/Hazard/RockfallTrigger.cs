using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §7.1 L5 — 落石ポイント。
/// SingingVase の大音量や、一定確率でトリガーされる。
/// 範囲内プレイヤーに落下ダメージを与える。
/// </summary>
public class RockfallTrigger : MonoBehaviour
{
    private static readonly List<RockfallTrigger> s_registeredTriggers = new();
    public static IReadOnlyList<RockfallTrigger> RegisteredTriggers => s_registeredTriggers;

    [Header("落石設定")]
    [SerializeField] private float  _triggerInterval   = 30f;   // 自然発生間隔（秒）
    [SerializeField] private float  _intervalVariance  = 15f;
    [SerializeField] private float  _rockDamage        = 25f;
    [SerializeField] private float  _rockSpeed         = 12f;
    [SerializeField] private int    _rockCount         = 3;
    [SerializeField] private float  _spreadRadius      = 4f;    // 落石の広がり半径
    [SerializeField] private bool   _autoTrigger       = true;

    [Header("岩 Prefab（null = 動的生成）")]
    [SerializeField] private GameObject _rockPrefab;

    private float _nextTriggerTime;

    public event System.Action OnActivated;

    private void OnEnable()
    {
        if (!s_registeredTriggers.Contains(this))
            s_registeredTriggers.Add(this);
    }

    private void OnDisable()
    {
        s_registeredTriggers.Remove(this);
    }

    private void Start()
    {
        if (_autoTrigger)
            ScheduleNext();
    }

    private void Update()
    {
        if (!_autoTrigger) return;
        if (Time.time >= _nextTriggerTime)
            Activate();
    }

    // ── トリガー ─────────────────────────────────────────────
    public void Activate()
    {
        StartCoroutine(SpawnRocks());
        OnActivated?.Invoke();

        if (_autoTrigger)
            ScheduleNext();
    }

    private void ScheduleNext()
    {
        _nextTriggerTime = Time.time + _triggerInterval + Random.Range(-_intervalVariance, _intervalVariance);
    }

    private IEnumerator SpawnRocks()
    {
        Debug.Log($"[Rockfall] 落石開始: {name}");

        for (int i = 0; i < _rockCount; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.1f, 0.4f));

            Vector3 offset = new Vector3(
                Random.Range(-_spreadRadius, _spreadRadius),
                0f,
                Random.Range(-_spreadRadius, _spreadRadius));

            Vector3 spawnPos = transform.position + offset;
            SpawnRock(spawnPos);
        }
    }

    private void SpawnRock(Vector3 position)
    {
        GameObject rock;
        if (_rockPrefab != null)
        {
            rock = Instantiate(_rockPrefab, position, Random.rotation);
        }
        else
        {
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.transform.position   = position;
            rock.transform.localScale = Vector3.one * Random.Range(0.3f, 0.7f);
            rock.GetComponent<Renderer>().material.color = new Color(0.5f, 0.45f, 0.4f);
        }

        var rb = rock.GetComponent<Rigidbody>() ?? rock.AddComponent<Rigidbody>();
        rb.linearVelocity = Vector3.down * _rockSpeed + Random.insideUnitSphere * 2f;
        rb.mass           = 5f;

        var dmg = rock.AddComponent<RockDamageOnCollision>();
        dmg.Damage = _rockDamage;

        Destroy(rock, 5f);
    }
}
