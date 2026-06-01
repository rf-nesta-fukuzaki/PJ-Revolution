using UnityEngine;

/// <summary>
/// プレイヤーの行動を物音として <see cref="NoiseEvent"/> に発信する（R.E.P.O. の音駆動エンカウンター）。
/// ダッシュ中は周期的に、ジャンプ時に単発で発信。Listener 型などの聴覚特化敵が反応する。
/// <see cref="ExplorerController"/> から自動付与される。
/// </summary>
[RequireComponent(typeof(ExplorerController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [SerializeField] private float _sprintNoiseRadius   = 20f;
    [SerializeField] private float _sprintNoiseInterval = 0.5f;
    [SerializeField] private float _jumpNoiseRadius      = 16f;

    private ExplorerController _controller;
    private float _timer;

    private void Awake() => _controller = GetComponent<ExplorerController>();

    private void Update()
    {
        _timer -= Time.deltaTime;

        if (_controller != null && _controller.enabled && _controller.IsSprinting && _timer <= 0f)
        {
            NoiseEvent.Emit(transform.position, _sprintNoiseRadius);
            _timer = _sprintNoiseInterval;
        }

        if (InputStateReader.JumpPressedThisFrame())
            NoiseEvent.Emit(transform.position, _jumpNoiseRadius);
    }
}
