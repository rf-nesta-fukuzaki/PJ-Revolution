using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <see cref="WireRopePhysics"/> の純関数を「GameObject / Rigidbody / リフレクション無し」で直接検証する。
/// Step B（God Class からの純関数抽出）の効果を示すテスト群。
/// </summary>
public sealed class WireRopePhysicsTests
{
    private WireRopeActionConfigSO _cfg;

    [SetUp]
    public void SetUp() => _cfg = ScriptableObject.CreateInstance<WireRopeActionConfigSO>();

    [TearDown]
    public void TearDown()
    {
        if (_cfg != null)
            Object.DestroyImmediate(_cfg);
    }

    // ── SurfaceApproachSpeed ──────────────────────────────────
    [Test]
    public void SurfaceApproachSpeed_PositiveWhenMovingIntoSurface()
    {
        float into = WireRopePhysics.SurfaceApproachSpeed(Vector3.up, Vector3.down * 3f);
        Assert.That(into, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void SurfaceApproachSpeed_ZeroWhenMovingAwayFromSurface()
    {
        float into = WireRopePhysics.SurfaceApproachSpeed(Vector3.up, Vector3.up * 3f);
        Assert.That(into, Is.EqualTo(0f).Within(0.001f));
    }

    // ── ShouldTriggerSlingshot（壁） ─────────────────────────
    [Test]
    public void Wall_TriggersWhenMovingIntoWall()
    {
        bool into = WireRopePhysics.ShouldTriggerSlingshot(Vector3.forward, Vector3.back * 3f, isGroundAnchor: false, _cfg);
        Assert.That(into, Is.True);
    }

    [Test]
    public void Wall_IgnoresVelocityMovingAway()
    {
        bool away = WireRopePhysics.ShouldTriggerSlingshot(Vector3.forward, Vector3.forward * 5f, isGroundAnchor: false, _cfg);
        Assert.That(away, Is.False);
    }

    // ── ShouldTriggerSlingshot（床・空中アンカー） ────────────
    [Test]
    public void Floor_TriggersWhenFallingIntoFloor()
    {
        bool falling = WireRopePhysics.ShouldTriggerSlingshot(Vector3.up, Vector3.down * 3f, isGroundAnchor: false, _cfg);
        Assert.That(falling, Is.True);
    }

    [Test]
    public void Floor_DoesNotTriggerWhenMovingUp()
    {
        bool up = WireRopePhysics.ShouldTriggerSlingshot(Vector3.up, Vector3.up * 3f, isGroundAnchor: false, _cfg);
        Assert.That(up, Is.False);
    }

    [Test]
    public void GroundAnchor_NaturalLandingDoesNotTrigger()
    {
        // 地面フックで足元の床に当たっただけ（自然な着地）は発動しない。
        bool landing = WireRopePhysics.ShouldTriggerSlingshot(Vector3.up, Vector3.down * 3f, isGroundAnchor: true, _cfg);
        Assert.That(landing, Is.False);
    }

    // ── ClampElevation ───────────────────────────────────────
    [Test]
    public void ClampElevation_LimitsSteepUpwardDirection()
    {
        Vector3 steepUp = new Vector3(1f, 5f, 0f); // ≈ 78.7°
        Vector3 clamped = WireRopePhysics.ClampElevation(steepUp, 18f, Vector3.forward, Vector3.forward);

        float elevationDeg = Mathf.Asin(Mathf.Clamp(clamped.y, -1f, 1f)) * Mathf.Rad2Deg;
        Assert.That(elevationDeg, Is.LessThanOrEqualTo(18.01f));
        Assert.That(clamped.x, Is.GreaterThan(0f));            // 水平方向は保持
        Assert.That(clamped.magnitude, Is.EqualTo(1f).Within(0.001f)); // 正規化されている
    }

    [Test]
    public void ClampElevation_KeepsHorizontalDirectionFlat()
    {
        Vector3 clamped = WireRopePhysics.ClampElevation(Vector3.right, 18f, Vector3.forward, Vector3.forward);
        Assert.That(clamped.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(clamped.x, Is.GreaterThan(0.99f));
    }

    // ── ComputeImpactLaunch ──────────────────────────────────
    [Test]
    public void ImpactLaunch_WallSpeedAtLeastBaseAndElevationClamped()
    {
        Vector3 v = WireRopePhysics.ComputeImpactLaunch(
            alongRope: Vector3.right, incomingVel: Vector3.right * 10f, surfaceNormal: Vector3.forward,
            clampMaxElevationDeg: 18f, retrieveRunDirXZ: Vector3.forward, flatForward: Vector3.forward,
            retrieveChargeFactor: 1f, _cfg);

        Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False);
        Assert.That(v.magnitude, Is.GreaterThanOrEqualTo(_cfg.ImpactSlingshotSpeed));

        float elevationDeg = Mathf.Asin(Mathf.Clamp(v.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        Assert.That(elevationDeg, Is.LessThanOrEqualTo(18.5f));
    }

    [Test]
    public void ImpactLaunch_FloorFallAddsUpwardPop()
    {
        // 床（法線=上）へ落下中の衝突は上向き成分（跳ね上げ）を持つ。
        Vector3 v = WireRopePhysics.ComputeImpactLaunch(
            alongRope: Vector3.right, incomingVel: new Vector3(5f, -5f, 0f), surfaceNormal: Vector3.up,
            clampMaxElevationDeg: 18f, retrieveRunDirXZ: Vector3.forward, flatForward: Vector3.forward,
            retrieveChargeFactor: 1f, _cfg);

        Assert.That(v.y, Is.GreaterThan(0f));
        Assert.That(v.magnitude, Is.GreaterThan(0f));
    }
}
