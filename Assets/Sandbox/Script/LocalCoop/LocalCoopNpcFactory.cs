using UnityEngine;

/// <summary>
/// オフライン NPC コンパニオンの生成（<see cref="OfflineNPCSpawner"/> と共有）。
/// </summary>
public static class LocalCoopNpcFactory
{
    public static GameObject SpawnNpc(
        int partySlotIndex,
        string displayName,
        Vector3 position,
        GameObject explorerModelPrefab,
        RuntimeAnimatorController animatorController,
        Vector3 modelOffset,
        Vector3 modelScale,
        Color? primitiveColor = null)
    {
        var root = new GameObject($"NPC_{displayName}");
        root.transform.position = position;

        var col = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.4f;
        col.center = Vector3.zero;

        var rb = root.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        root.AddComponent<NPCController>();

        var member = root.AddComponent<LocalCoopPartyMember>();
        member.Configure(partySlotIndex, isHuman: false, displayName);

        if (explorerModelPrefab != null)
            AttachExplorerModel(root.transform, explorerModelPrefab, animatorController, modelOffset, modelScale);
        else
        {
            AttachPrimitiveVisual(root.transform, primitiveColor ?? Color.HSVToRGB(partySlotIndex / 6f, 0.7f, 0.9f));
            if (animatorController != null)
            {
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        return root;
    }

    private static void AttachExplorerModel(
        Transform parent,
        GameObject explorerModelPrefab,
        RuntimeAnimatorController animatorController,
        Vector3 modelOffset,
        Vector3 modelScale)
    {
        var modelGo = Object.Instantiate(explorerModelPrefab, parent);
        modelGo.name = "ExplorerModel";
        modelGo.transform.localPosition = modelOffset;
        modelGo.transform.localRotation = Quaternion.identity;
        modelGo.transform.localScale = modelScale;

        foreach (var col in modelGo.GetComponentsInChildren<Collider>())
            Object.Destroy(col);

        var animator = modelGo.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = modelGo.AddComponent<Animator>();

        if (animatorController != null)
            animator.runtimeAnimatorController = animatorController;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    private static void AttachPrimitiveVisual(Transform parent, Color bodyColor)
    {
        BuildPrimitive(PrimitiveType.Capsule, parent, Vector3.zero, new Vector3(0.8f, 0.9f, 0.8f), bodyColor);
        BuildPrimitive(PrimitiveType.Sphere, parent, new Vector3(0f, 1.1f, 0f), Vector3.one * 0.35f,
            Color.Lerp(bodyColor, Color.white, 0.45f));
    }

    private static void BuildPrimitive(
        PrimitiveType type,
        Transform parent,
        Vector3 localPos,
        Vector3 localScale,
        Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
