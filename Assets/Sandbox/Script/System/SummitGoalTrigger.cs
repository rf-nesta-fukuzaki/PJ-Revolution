using PeakPlunder.Audio;
using UnityEngine;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// Stage01 mountain summit trigger. Entering the summit ends the expedition
/// through the existing ExpeditionManager return flow.
/// </summary>
public class SummitGoalTrigger : MonoBehaviour
{
    [Header("演出")]
    [SerializeField] private ParticleSystem _celebrationParticles;
    [SerializeField] private float _activationDelay = 1.5f;

    private bool _triggered;

    private void Awake()
    {
        EnsureTriggerCollider();
        TrySetTag("SummitGoal");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        Debug.Log("[SummitGoal] 山頂到達！");

        if (_celebrationParticles != null)
            _celebrationParticles.Play();

        GameServices.Audio?.PlaySE(SoundId.Summit, transform.position);
        Invoke(nameof(TriggerReturn), _activationDelay);
    }

    private void TriggerReturn()
    {
        GameServices.Expedition?.ReturnToBase(allSurvived: true);
    }

    private void EnsureTriggerCollider()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 8f;
            col = sphere;
        }

        col.isTrigger = true;
    }

    private void TrySetTag(string tagName)
    {
        try
        {
            gameObject.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[SummitGoal] Tag '{tagName}' is not defined. Add it in Project Settings.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, 8f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 8f);
    }
}
