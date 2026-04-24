using UnityEngine;

public enum NpcMetaGoal
{
    Explore = 0,
    SecureRelic = 1,
    ReturnRelic = 2,
    Recover = 3,
    AssistTeammate = 4,
    Repath = 5
}

public readonly struct NpcMetaContext
{
    public NpcMetaContext(
        bool hasRelic,
        float hpRatio,
        float threatLevel,
        float expeditionUrgency,
        float nearestRelicDistance,
        float nearestReturnZoneDistance,
        float nearestShelterDistance,
        float nearestTeammateDistance,
        bool allyInDanger,
        float stuckSeconds)
    {
        HasRelic = hasRelic;
        HpRatio = Mathf.Clamp01(hpRatio);
        ThreatLevel = Mathf.Clamp01(threatLevel);
        ExpeditionUrgency = Mathf.Clamp01(expeditionUrgency);
        NearestRelicDistance = SanitizeDistance(nearestRelicDistance);
        NearestReturnZoneDistance = SanitizeDistance(nearestReturnZoneDistance);
        NearestShelterDistance = SanitizeDistance(nearestShelterDistance);
        NearestTeammateDistance = SanitizeDistance(nearestTeammateDistance);
        AllyInDanger = allyInDanger;
        StuckSeconds = Mathf.Max(0f, stuckSeconds);
    }

    public bool HasRelic { get; }
    public float HpRatio { get; }
    public float ThreatLevel { get; }
    public float ExpeditionUrgency { get; }
    public float NearestRelicDistance { get; }
    public float NearestReturnZoneDistance { get; }
    public float NearestShelterDistance { get; }
    public float NearestTeammateDistance { get; }
    public bool AllyInDanger { get; }
    public float StuckSeconds { get; }

    public bool HasRelicCandidate => !float.IsInfinity(NearestRelicDistance);
    public bool HasReturnZone => !float.IsInfinity(NearestReturnZoneDistance);
    public bool HasShelter => !float.IsInfinity(NearestShelterDistance);
    public bool HasTeammate => !float.IsInfinity(NearestTeammateDistance);

    private static float SanitizeDistance(float v)
    {
        if (float.IsNaN(v) || v < 0f) return Mathf.Infinity;
        return v;
    }
}

public readonly struct NpcMetaDecision
{
    public NpcMetaDecision(NpcMetaGoal goal, float score, string reason)
    {
        Goal = goal;
        Score = score;
        Reason = reason ?? string.Empty;
    }

    public NpcMetaGoal Goal { get; }
    public float Score { get; }
    public string Reason { get; }
}

/// <summary>
/// NPC の高レベル意思決定（メタAI）を担当するユーティリティ評価器。
/// 各フレームの移動は NPCController に委譲し、ここでは優先目標のみを返す。
/// </summary>
public sealed class NPCMetaDecisionEngine
{
    public NpcMetaDecision Decide(in NpcMetaContext context)
    {
        float lowHpPressure = 1f - context.HpRatio;
        float recoverPressure = Mathf.Clamp01((lowHpPressure * 1.2f) + (context.ThreatLevel * 0.8f));

        float returnScore = ScoreReturnRelic(context, lowHpPressure);
        float secureScore = ScoreSecureRelic(context, lowHpPressure);
        float recoverScore = ScoreRecover(context, recoverPressure);
        float assistScore = ScoreAssist(context);
        float repathScore = ScoreRepath(context);
        float exploreScore = ScoreExplore(context);

        var best = new NpcMetaDecision(NpcMetaGoal.Explore, exploreScore, "探索を継続");
        best = PickHigher(best, new NpcMetaDecision(NpcMetaGoal.ReturnRelic, returnScore, "遺物搬送を優先"));
        best = PickHigher(best, new NpcMetaDecision(NpcMetaGoal.SecureRelic, secureScore, "遺物確保を優先"));
        best = PickHigher(best, new NpcMetaDecision(NpcMetaGoal.Recover, recoverScore, "安全回復を優先"));
        best = PickHigher(best, new NpcMetaDecision(NpcMetaGoal.AssistTeammate, assistScore, "味方支援を優先"));
        best = PickHigher(best, new NpcMetaDecision(NpcMetaGoal.Repath, repathScore, "経路再計算を優先"));
        return best;
    }

    private static NpcMetaDecision PickHigher(in NpcMetaDecision current, in NpcMetaDecision candidate)
    {
        return candidate.Score > current.Score ? candidate : current;
    }

    private static float ScoreReturnRelic(in NpcMetaContext context, float lowHpPressure)
    {
        if (!context.HasRelic || !context.HasReturnZone)
            return float.NegativeInfinity;

        return 1.20f
               + (DistanceFactor(context.NearestReturnZoneDistance, 40f) * 0.60f)
               + (context.ExpeditionUrgency * 0.35f)
               + (context.ThreatLevel * 0.25f)
               + (lowHpPressure * 0.20f);
    }

    private static float ScoreSecureRelic(in NpcMetaContext context, float lowHpPressure)
    {
        if (context.HasRelic || !context.HasRelicCandidate)
            return float.NegativeInfinity;

        return 0.58f
               + (DistanceFactor(context.NearestRelicDistance, 28f) * 0.80f)
               + (context.ExpeditionUrgency * 0.22f)
               - (context.ThreatLevel * 0.35f)
               - (lowHpPressure * 0.45f);
    }

    private static float ScoreRecover(in NpcMetaContext context, float recoverPressure)
    {
        float shelterBonus = context.HasShelter
            ? DistanceFactor(context.NearestShelterDistance, 20f) * 0.55f
            : -0.20f;

        return 0.20f
               + (recoverPressure * 1.05f)
               + shelterBonus
               - (context.ExpeditionUrgency * 0.15f);
    }

    private static float ScoreAssist(in NpcMetaContext context)
    {
        if (!context.HasTeammate)
            return float.NegativeInfinity;

        float proximity = DistanceFactor(context.NearestTeammateDistance, 24f);
        float dangerBoost = context.AllyInDanger ? 0.75f : 0f;
        return 0.20f + (proximity * 0.40f) + dangerBoost + (context.HpRatio * 0.15f);
    }

    private static float ScoreRepath(in NpcMetaContext context)
    {
        if (context.StuckSeconds < 1.2f)
            return float.NegativeInfinity;

        return 0.95f + Mathf.Clamp01(context.StuckSeconds / 6f) * 0.25f;
    }

    private static float ScoreExplore(in NpcMetaContext context)
    {
        return 0.35f
               + (context.ExpeditionUrgency * 0.25f)
               + (context.HpRatio * 0.15f)
               - (context.ThreatLevel * 0.20f);
    }

    private static float DistanceFactor(float distance, float cap)
    {
        if (float.IsInfinity(distance))
            return 0f;

        if (cap <= 0.001f)
            return 0f;

        return 1f - Mathf.Clamp01(distance / cap);
    }
}
