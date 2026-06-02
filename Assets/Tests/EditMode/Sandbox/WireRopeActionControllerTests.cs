using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WireRopeActionControllerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject _root;
    private Rigidbody _rb;
    private WireRopeActionController _controller;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("wire-rope-test-player");
        _rb = _root.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _root.AddComponent<CapsuleCollider>();
        _controller = _root.AddComponent<WireRopeActionController>();
        MoveBody(Vector3.zero);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void RaisedGroundAnchor_UsesUpwardPullDirection()
    {
        ConfigureAnchor(new Vector3(8f, 4f, 0f), isGround: true);

        Vector3 dir = Invoke<Vector3>("GetPhysicsPullDirection");

        Assert.That(dir.x, Is.GreaterThan(0.85f));
        Assert.That(dir.y, Is.GreaterThan(0.05f));
    }

    [Test]
    public void FlatGroundAnchor_KeepsPullDirectionHorizontal()
    {
        ConfigureAnchor(new Vector3(8f, 0.5f, 0f), isGround: true);

        Vector3 dir = Invoke<Vector3>("GetPhysicsPullDirection");

        Assert.That(dir.x, Is.GreaterThan(0.99f));
        Assert.That(dir.y, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void RaisedGroundAnchor_UsesThreeDimensionalStretch()
    {
        ConfigureAnchor(new Vector3(0f, 6f, 8f), isGround: true);
        Set("_chargedThrowRange", 10f);

        float stretch = Invoke<float>("GetRopeStretch01");

        Assert.That(stretch, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void RaisedGroundAnchor_MinPullSpeedKeepsVerticalComponent()
    {
        ConfigureAnchor(new Vector3(8f, 4f, 0f), isGround: true);
        Set("_targetPullSpeed", 20f);
        Set("_pullFloorFraction", 0.5f);
        Set("_tensionFalloffFloor", 0.2f);
        _rb.linearVelocity = Vector3.zero;

        InvokeVoid("ApplyMinPullSpeed", new Vector3(8f, 4f, 0f).normalized, Vector3.zero, 1f);

        Assert.That(_rb.linearVelocity.x, Is.GreaterThan(0.5f));
        Assert.That(_rb.linearVelocity.y, Is.GreaterThan(0.05f));
    }

    [Test]
    public void AirRope_FloorImpactSlingshotTriggersOnlyWhenMovingIntoFloor()
    {
        ConfigureAnchor(new Vector3(0f, 8f, 8f), isGround: false);

        bool fallingIntoFloor = Invoke<bool>("ShouldTriggerSlingshotFromContact", Vector3.up, Vector3.down * 3f);
        bool movingAwayFromFloor = Invoke<bool>("ShouldTriggerSlingshotFromContact", Vector3.up, Vector3.up * 3f);

        Assert.That(fallingIntoFloor, Is.True);
        Assert.That(movingAwayFromFloor, Is.False);
    }

    [Test]
    public void AirRope_WallImpactIgnoresVelocityMovingAway()
    {
        ConfigureAnchor(new Vector3(0f, 8f, 8f), isGround: false);

        bool movingAwayFromWall = Invoke<bool>("ShouldTriggerSlingshotFromContact", Vector3.forward, Vector3.forward * 5f);
        bool movingIntoWall = Invoke<bool>("ShouldTriggerSlingshotFromContact", Vector3.forward, Vector3.back * 3f);

        Assert.That(movingAwayFromWall, Is.False);
        Assert.That(movingIntoWall, Is.True);
    }

    [Test]
    public void PlainCollider_DoesNotEnableTargetPull()
    {
        var obj = new GameObject("plain-rock");
        try
        {
            var col = obj.AddComponent<BoxCollider>();

            InvokeVoid("ResolvePullTarget", col);

            Assert.That(Get<bool>("_pullTargetToPlayer"), Is.False);
            Assert.That(Get<Rigidbody>("_attachedTargetBody"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(obj);
        }
    }

    [Test]
    public void PlayerTaggedBody_EnablesTargetPull()
    {
        var obj = new GameObject("other-character") { tag = "Player" };
        try
        {
            var body = obj.AddComponent<Rigidbody>();
            var col = obj.AddComponent<BoxCollider>();

            InvokeVoid("ResolvePullTarget", col);

            Assert.That(Get<bool>("_pullTargetToPlayer"), Is.True);
            Assert.That(Get<Rigidbody>("_attachedTargetBody"), Is.SameAs(body));
        }
        finally
        {
            Object.DestroyImmediate(obj);
        }
    }

    [Test]
    public void NullCollider_ClearsTargetPull()
    {
        Set<bool>("_pullTargetToPlayer", true);

        InvokeVoid("ResolvePullTarget", new object[] { null });

        Assert.That(Get<bool>("_pullTargetToPlayer"), Is.False);
    }

    private void ConfigureAnchor(Vector3 anchor, bool isGround)
    {
        Set("_anchorPoint", anchor);
        Set("_anchorIsGround", isGround);
        Set("_groundClimbLiftThreshold", 1.5f);
    }

    private void MoveBody(Vector3 position)
    {
        _root.transform.position = position;
        _rb.position = position;
        Physics.SyncTransforms();
    }

    private void Set<T>(string fieldName, T value)
    {
        FieldInfo field = typeof(WireRopeActionController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(_controller, value);
    }

    private T Get<T>(string fieldName)
    {
        FieldInfo field = typeof(WireRopeActionController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        return (T)field.GetValue(_controller);
    }

    private T Invoke<T>(string methodName, params object[] args)
    {
        MethodInfo method = typeof(WireRopeActionController).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, $"Missing method: {methodName}");
        return (T)method.Invoke(_controller, args);
    }

    private void InvokeVoid(string methodName, params object[] args)
    {
        MethodInfo method = typeof(WireRopeActionController).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, $"Missing method: {methodName}");
        method.Invoke(_controller, args);
    }
}
