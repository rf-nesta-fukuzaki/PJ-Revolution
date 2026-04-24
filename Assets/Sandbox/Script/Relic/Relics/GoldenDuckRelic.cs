using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §6.2 — 遺物①「黄金のアヒル像」
/// 物理軸：転がる。丸みがあり坂で延々転がる入門遺物。
/// 難易度：★☆☆  壊れやすさ：低
/// </summary>
public class GoldenDuckRelic : RelicBase
{
    [Header("アヒル設定")]
    [SerializeField] private float _rollSpeedThreshold = 1.5f;   // この速さ以上で転がりSEを鳴らす
    [SerializeField] private float _rollSoundInterval  = 0.3f;

    private AudioSource _audioSource;
    private float       _rollSoundTimer;
    private bool        _isRolling;

    protected override void Awake()
    {
        // base.Awake() が _currentHp = _maxHp を実行するため、
        // フィールドは必ず先に設定する（後置では _currentHp が古い値で初期化される）
        _relicName        = "黄金のアヒル像";
        _baseValue        = 80;
        _maxHp            = 100f;
        _damageMultiplier = 0.5f;   // 壊れにくい
        _impactThreshold  = 3f;

        base.Awake();

        // アヒル固有の物理設定（base.Awake で _rb が初期化された後）
        _rb.angularDamping = 0.2f;
        _audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (_isDestroyed) return;
        CheckRolling();
    }

    private void CheckRolling()
    {
        float speed = _rb.linearVelocity.magnitude;
        _isRolling = speed > _rollSpeedThreshold && !_isHeld;

        if (!_isRolling) return;

        _rollSoundTimer -= Time.deltaTime;
        if (_rollSoundTimer > 0f) return;

        _rollSoundTimer = _rollSoundInterval;
        PlayRollSound();
    }

    private void PlayRollSound()
    {
        // GDD §15.2 — relic_duck_roll（アヒル転がり音）
        PPAudioManager.Instance?.PlaySE(SoundId.RelicDuckRoll, transform.position);

        if (_audioSource == null) return;
        // AudioClip 未アサイン時は無音で通過
        if (_audioSource.clip == null) return;
        _audioSource.pitch = Random.Range(0.9f, 1.1f);
        _audioSource.PlayOneShot(_audioSource.clip, 0.4f);
    }

    protected override Color GizmoColor => new Color(0.83f, 0.68f, 0.21f);

    protected override void BuildVisual()
    {
        var gold   = new Color(0.83f, 0.68f, 0.21f);
        var orange = new Color(1.00f, 0.55f, 0.05f);
        var dark   = new Color(0.08f, 0.08f, 0.08f);

        // 胴体
        VizChild(PrimitiveType.Sphere, "body",
            new Vector3(0f, 0.05f, 0f), new Vector3(1.6f, 1.3f, 1.1f),
            gold, metallic: 0.5f, smoothness: 0.8f);
        // 頭
        VizChild(PrimitiveType.Sphere, "head",
            new Vector3(0.62f, 0.68f, 0f), new Vector3(0.88f, 0.88f, 0.88f),
            gold, metallic: 0.5f, smoothness: 0.8f);
        // くちばし
        VizChild(PrimitiveType.Cube, "beak",
            new Vector3(0.98f, 0.66f, 0f), new Vector3(0.48f, 0.22f, 0.25f),
            orange, smoothness: 0.4f);
        // 尾
        VizChildRot(PrimitiveType.Cube, "tail",
            new Vector3(-0.75f, 0.45f, 0f),
            Quaternion.Euler(38f, 0f, 0f),
            new Vector3(0.28f, 0.55f, 0.2f),
            gold, metallic: 0.5f, smoothness: 0.7f);
        // 目
        VizChild(PrimitiveType.Sphere, "eye_L",
            new Vector3(0.78f, 0.73f, 0.30f), new Vector3(0.18f, 0.18f, 0.10f),
            dark, smoothness: 0.9f);
        VizChild(PrimitiveType.Sphere, "eye_R",
            new Vector3(0.78f, 0.73f, -0.30f), new Vector3(0.18f, 0.18f, 0.10f),
            dark, smoothness: 0.9f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        Debug.Log("[GoldenDuck] アヒルが壊れた！「なんでアヒルを崇拝するんだこの文明」");
    }
}
