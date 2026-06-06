using System.Collections;
using PeakPlunder.Audio;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// グラップリングフックの命中状態を NGO で同期する（オーナーは物理、他クライアントはビジュアルのみ）。
/// </summary>
public class NetworkGrapplingHookSync : NetworkBehaviour
{
    private readonly NetworkVariable<bool> _isGrappling = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> _anchorPoint = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> _lineLength = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject _remoteHookVisual;
    private Coroutine  _remoteFlyRoutine;

    public bool IsGrappling  => _isGrappling.Value;
    public Vector3 AnchorPoint => _anchorPoint.Value;

    public override void OnNetworkSpawn()
    {
        _isGrappling.OnValueChanged += HandleGrappleStateChanged;
        if (_isGrappling.Value)
            PlaceRemoteHookAtAnchor(_anchorPoint.Value);
    }

    public override void OnNetworkDespawn()
    {
        _isGrappling.OnValueChanged -= HandleGrappleStateChanged;
        ClearRemoteVisual();
    }

    public void ReportGrappleStarted(Vector3 origin, Vector3 anchor, float lineLength)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        if (IsServer)
            ApplyGrappleState(true, anchor, lineLength, origin);
        else
            ReportGrappleStartedServerRpc(origin, anchor, lineLength);
    }

    public void ReportGrappleReleased()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        if (IsServer)
            ApplyGrappleState(false, Vector3.zero, 0f, Vector3.zero);
        else
            ReportGrappleReleasedServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReportGrappleStartedServerRpc(Vector3 origin, Vector3 anchor, float lineLength)
    {
        ApplyGrappleState(true, anchor, lineLength, origin);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReportGrappleReleasedServerRpc()
    {
        ApplyGrappleState(false, Vector3.zero, 0f, Vector3.zero);
    }

    private void ApplyGrappleState(bool grappling, Vector3 anchor, float lineLength, Vector3 origin)
    {
        _isGrappling.Value  = grappling;
        _anchorPoint.Value  = anchor;
        _lineLength.Value   = lineLength;

        if (grappling)
            NotifyGrappleVisualClientRpc(origin, anchor, lineLength);
        else
            NotifyGrappleReleaseClientRpc();
    }

    [ClientRpc]
    private void NotifyGrappleVisualClientRpc(Vector3 origin, Vector3 anchor, float lineLength)
    {
        if (IsOwner)
            return;

        ShowRemoteGrappleVisual(origin, anchor, lineLength);
    }

    [ClientRpc]
    private void NotifyGrappleReleaseClientRpc()
    {
        if (IsOwner)
            return;

        ClearRemoteVisual();
    }

    private void HandleGrappleStateChanged(bool previous, bool current)
    {
        if (IsOwner)
            return;

        if (!current)
            ClearRemoteVisual();
    }

    private void PlaceRemoteHookAtAnchor(Vector3 anchor)
    {
        ClearRemoteVisual();

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "RemoteGrappleHook";
        var col = vis.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        vis.transform.position   = anchor;
        vis.transform.localScale = new Vector3(0.12f, 0.12f, 0.28f);
        _remoteHookVisual = vis;
    }

    private void ShowRemoteGrappleVisual(Vector3 origin, Vector3 anchor, float lineLength)
    {
        ClearRemoteVisual();

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "RemoteGrappleHook";
        var col = vis.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        vis.transform.position   = origin;
        vis.transform.localScale = new Vector3(0.12f, 0.12f, 0.28f);
        vis.transform.rotation   = Quaternion.LookRotation(
            (anchor - origin).sqrMagnitude > 0.001f ? (anchor - origin).normalized : Vector3.forward);

        _remoteHookVisual = vis;
        _remoteFlyRoutine = StartCoroutine(RemoteHookFlyRoutine(vis, origin, anchor));
    }

    private IEnumerator RemoteHookFlyRoutine(GameObject vis, Vector3 origin, Vector3 target)
    {
        const float flySpeed = 40f;
        float dist     = Vector3.Distance(origin, target);
        float duration = dist / Mathf.Max(flySpeed, 0.1f);
        float elapsed  = 0f;

        while (elapsed < duration && vis != null && _isGrappling.Value)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            vis.transform.position = Vector3.Lerp(origin, target, t);
            yield return null;
        }

        if (vis != null && _isGrappling.Value)
        {
            vis.transform.position = target;
            GameServices.Audio?.PlaySE(SoundId.GrapplingHit, target);
        }

        _remoteFlyRoutine = null;
    }

    private void ClearRemoteVisual()
    {
        if (_remoteFlyRoutine != null)
        {
            StopCoroutine(_remoteFlyRoutine);
            _remoteFlyRoutine = null;
        }

        if (_remoteHookVisual != null)
        {
            Object.Destroy(_remoteHookVisual);
            _remoteHookVisual = null;
        }
    }
}
