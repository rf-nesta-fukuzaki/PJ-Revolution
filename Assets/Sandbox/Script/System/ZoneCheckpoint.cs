using PeakPlunder.Audio;
using UnityEngine;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// Stage01 zone boundary checkpoint. It updates ExpeditionManager once when
/// the player crosses a zone checkpoint trigger.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ZoneCheckpoint : MonoBehaviour
{
    [Header("チェックポイント設定")]
    [SerializeField] private int _checkpointIndex;
    [SerializeField] private float _triggerRadius = 5f;

    [Header("演出")]
    [SerializeField] private ParticleSystem _passParticles;

    private bool _passed;

    private void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.radius = _triggerRadius;
        col.isTrigger = true;
        TrySetTag("Checkpoint");
    }

    private void OnValidate()
    {
        _triggerRadius = Mathf.Max(0.1f, _triggerRadius);

        var col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.radius = _triggerRadius;
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_passed) return;
        if (!other.CompareTag("Player")) return;

        _passed = true;
        GameServices.Expedition?.OnCheckpointReached(_checkpointIndex);

        // 拠点⇄当該チェックポイントのジップラインを開通させ、以後は登りをショートカットできるようにする。
        Sandbox.World.Zipline.ZiplineNetwork.Ensure().InstallLine(_checkpointIndex, transform.position);

        if (_passParticles != null)
            _passParticles.Play();

        GameServices.Audio?.PlaySE(SoundId.Checkpoint, transform.position);
        Debug.Log($"[Checkpoint] Checkpoint {_checkpointIndex + 1} 通過");
    }

    private void TrySetTag(string tagName)
    {
        try
        {
            gameObject.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[Checkpoint] Tag '{tagName}' is not defined. Add it in Project Settings.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(transform.position, _triggerRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _triggerRadius);
    }
}
