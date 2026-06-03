using UnityEngine;

/// <summary>
/// ワイヤーロープの「純粋な物理計算」を集約した static クラス。
///
/// 設計意図（Step B: God Class からの純関数抽出）:
///   ・Rigidbody / GameObject / Transform に依存しない純関数のみを置く
///   ・幾何（アンカー・体位置）は呼び出し側で解決し、結果と設定値(SO)を引数で渡す
///   ・これにより EditMode テストでリフレクション不要・GameObject 不要で直接検証できる
///
/// <see cref="WireRopeActionController"/> の該当メソッドはここへ委譲する（薄いラッパ）。
/// </summary>
public static class WireRopePhysics
{
    /// <summary>接地とみなす法線の Y 下限（<see cref="WireRopeActionController"/> と共有）。</summary>
    public const float GroundNormalThreshold = 0.65f;

    /// <summary>水平成分のみ（Y を 0 に）。</summary>
    public static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>NaN / Infinity を含まないか（不変条件チェック用）。</summary>
    public static bool IsFinite(Vector3 v)
        => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
            || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    /// <summary>面法線に沿った「めり込み速度」（負の内積。0 でクランプ）。</summary>
    public static float SurfaceApproachSpeed(Vector3 normal, Vector3 velocity)
    {
        if (normal.sqrMagnitude < 0.01f)
            return 0f;

        normal.Normalize();
        return Mathf.Max(0f, -Vector3.Dot(velocity, normal));
    }

    /// <summary>壁・岩など（=障害物）への接触でスリングショットを発動すべきか。</summary>
    public static bool IsObstacleContactNormal(Vector3 normal, Vector3 approachVelocity, WireRopeActionConfigSO cfg)
    {
        if (normal.sqrMagnitude < 0.01f)
            return false;

        normal.Normalize();
        float into = SurfaceApproachSpeed(normal, approachVelocity);
        if (normal.y < cfg.ObstacleNormalMaxY)
            return into >= cfg.ImpactSlingshotMinIntoSpeed;

        if (into >= cfg.ImpactSlingshotHardImpactSpeed)
            return true;

        if (normal.y < 0.72f && into >= cfg.ImpactSlingshotMinIntoSpeed)
            return true;

        return false;
    }

    /// <summary>床（=ほぼ水平面）への接触でスリングショットを発動すべきか。</summary>
    public static bool IsFloorSlingshotContact(Vector3 normal, Vector3 approachVelocity, bool isGroundAnchor, WireRopeActionConfigSO cfg)
    {
        if (normal.sqrMagnitude < 0.01f)
            return false;

        normal.Normalize();
        if (normal.y < GroundNormalThreshold)
            return false;

        float into = SurfaceApproachSpeed(normal, approachVelocity);
        if (into >= cfg.FloorSlingshotMinIntoSpeed)
            return true;

        if (isGroundAnchor)
            return false;

        return Flatten(approachVelocity).magnitude >= cfg.FloorSlingshotMinHorizSpeed;
    }

    /// <summary>接触面と進入速度からスリングショット発動可否を総合判定する。</summary>
    public static bool ShouldTriggerSlingshot(Vector3 normal, Vector3 approachVelocity, bool isGroundAnchor, WireRopeActionConfigSO cfg)
    {
        if (normal.sqrMagnitude > 0.01f)
            normal.Normalize();

        // 地面アンカーで足元の床に当たっただけなら発動しない（自然な着地）。
        if (isGroundAnchor && normal.y >= GroundNormalThreshold)
            return false;

        if (IsObstacleContactNormal(normal, approachVelocity, cfg))
            return true;

        return IsFloorSlingshotContact(normal, approachVelocity, isGroundAnchor, cfg);
    }

    /// <summary>
    /// 引き方向の仰角を上限以内へクランプする（真上への吹き上がり防止）。
    /// 仰角上限はアンカー幾何に依存するため呼び出し側で計算して渡す。
    /// </summary>
    public static Vector3 ClampElevation(Vector3 dir, float maxElevationDeg, Vector3 retrieveRunDirXZ, Vector3 flatForward)
    {
        Contract.Requires(maxElevationDeg >= 0f && maxElevationDeg < 90f,
            $"ClampElevation: maxElevationDeg は [0,90) であること (実際 {maxElevationDeg})");

        if (dir.sqrMagnitude < 0.0001f)
            return flatForward;

        dir.Normalize();
        Vector3 flat = Flatten(dir);
        if (flat.sqrMagnitude < 0.0001f)
        {
            flat = Flatten(retrieveRunDirXZ);
            if (flat.sqrMagnitude < 0.0001f)
                flat = flatForward;
        }

        flat.Normalize();
        float maxUp = Mathf.Tan(maxElevationDeg * Mathf.Deg2Rad);
        float up = Mathf.Clamp(dir.y, -maxUp * 0.25f, maxUp);
        return new Vector3(flat.x, up, flat.z).normalized;
    }

    /// <summary>
    /// 障害物衝突時のスリングショット打ち上げ速度を計算する。
    /// alongRope（プレイヤー→アンカーの引き方向）と仰角上限は呼び出し側で解決して渡す。
    /// </summary>
    public static Vector3 ComputeImpactLaunch(
        Vector3 alongRope, Vector3 incomingVel, Vector3 surfaceNormal,
        float clampMaxElevationDeg, Vector3 retrieveRunDirXZ, Vector3 flatForward,
        float retrieveChargeFactor, WireRopeActionConfigSO cfg)
    {
        Contract.RequiresNotNull(cfg, nameof(cfg));

        Vector3 launchDir = alongRope;

        if (incomingVel.sqrMagnitude > 0.25f && surfaceNormal.sqrMagnitude > 0.01f)
        {
            Vector3 reflected = Vector3.Reflect(incomingVel, surfaceNormal.normalized);
            if (reflected.sqrMagnitude > 0.01f)
                launchDir = Vector3.Slerp(alongRope, reflected.normalized, 0.4f).normalized;
        }

        if (surfaceNormal.y >= GroundNormalThreshold)
        {
            launchDir = Flatten(launchDir);
            if (launchDir.sqrMagnitude < 0.01f)
                launchDir = Flatten(alongRope);
            if (launchDir.sqrMagnitude < 0.01f)
                launchDir = flatForward;

            launchDir = launchDir.normalized;
            if (incomingVel.y < -2f)
                launchDir = (launchDir + Vector3.up * 0.2f).normalized;
        }
        else
        {
            launchDir = ClampElevation(launchDir, clampMaxElevationDeg, retrieveRunDirXZ, flatForward);
        }

        float carry = incomingVel.magnitude * cfg.ImpactSlingshotCarryFactor;
        float launchSpeed = Mathf.Max(cfg.ImpactSlingshotSpeed, carry + cfg.ImpactSlingshotSpeed * 0.5f);
        launchSpeed *= cfg.ImpactRestitution * Mathf.Lerp(1f, 1.06f, retrieveChargeFactor - 1f);
        Vector3 result = launchDir * launchSpeed;

        if (surfaceNormal.y >= GroundNormalThreshold && incomingVel.y < -3f)
            result += Vector3.up * cfg.ImpactSlingshotFloorPopUp;

        Vector3 launch = ClampElevation(result.normalized, clampMaxElevationDeg, retrieveRunDirXZ, flatForward) * result.magnitude;
        Contract.Ensures(IsFinite(launch), "ComputeImpactLaunch: 結果に NaN/Inf を含まないこと");
        return launch;
    }
}
