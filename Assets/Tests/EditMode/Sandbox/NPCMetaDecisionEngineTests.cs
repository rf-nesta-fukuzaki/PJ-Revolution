using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class NPCMetaDecisionEngineTests
{
    [Test]
    public void Decide_PrioritizesReturn_WhenRelicIsCarried()
    {
        object decision = InvokeDecide(
            hasRelic: true,
            hpRatio: 0.85f,
            threatLevel: 0.15f,
            expeditionUrgency: 0.25f,
            nearestRelicDistance: 12f,
            nearestReturnZoneDistance: 8f,
            nearestShelterDistance: 15f,
            nearestTeammateDistance: 20f,
            allyInDanger: false,
            stuckSeconds: 0f);

        Assert.That(ReadGoalName(decision), Is.EqualTo("ReturnRelic"));
    }

    [Test]
    public void Decide_PrioritizesRecover_WhenLowHpOrHighThreat()
    {
        object decision = InvokeDecide(
            hasRelic: false,
            hpRatio: 0.18f,
            threatLevel: 0.82f,
            expeditionUrgency: 0.4f,
            nearestRelicDistance: 6f,
            nearestReturnZoneDistance: 24f,
            nearestShelterDistance: 5f,
            nearestTeammateDistance: 10f,
            allyInDanger: false,
            stuckSeconds: 0.5f);

        Assert.That(ReadGoalName(decision), Is.EqualTo("Recover"));
    }

    [Test]
    public void Decide_PrioritizesSecureRelic_WhenRelicIsNearby()
    {
        object decision = InvokeDecide(
            hasRelic: false,
            hpRatio: 0.9f,
            threatLevel: 0.2f,
            expeditionUrgency: 0.55f,
            nearestRelicDistance: 3.2f,
            nearestReturnZoneDistance: 30f,
            nearestShelterDistance: 18f,
            nearestTeammateDistance: 7f,
            allyInDanger: false,
            stuckSeconds: 0f);

        Assert.That(ReadGoalName(decision), Is.EqualTo("SecureRelic"));
    }

    [Test]
    public void Decide_PrioritizesAssist_WhenAllyInDanger()
    {
        object decision = InvokeDecide(
            hasRelic: false,
            hpRatio: 0.75f,
            threatLevel: 0.25f,
            expeditionUrgency: 0.35f,
            nearestRelicDistance: 22f,
            nearestReturnZoneDistance: 35f,
            nearestShelterDistance: 30f,
            nearestTeammateDistance: 6f,
            allyInDanger: true,
            stuckSeconds: 0f);

        Assert.That(ReadGoalName(decision), Is.EqualTo("AssistTeammate"));
    }

    [Test]
    public void Decide_FallsBackToExplore_WhenNoStrongSignal()
    {
        object decision = InvokeDecide(
            hasRelic: false,
            hpRatio: 0.95f,
            threatLevel: 0.05f,
            expeditionUrgency: 0.05f,
            nearestRelicDistance: Mathf.Infinity,
            nearestReturnZoneDistance: 45f,
            nearestShelterDistance: Mathf.Infinity,
            nearestTeammateDistance: 16f,
            allyInDanger: false,
            stuckSeconds: 0f);

        Assert.That(ReadGoalName(decision), Is.EqualTo("Explore"));
    }

    private static object InvokeDecide(
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
        Type engineType = ResolveType("NPCMetaDecisionEngine");
        Type contextType = ResolveType("NpcMetaContext");

        Assert.That(engineType, Is.Not.Null, "NPCMetaDecisionEngine type not found.");
        Assert.That(contextType, Is.Not.Null, "NpcMetaContext type not found.");

        object engine = Activator.CreateInstance(engineType);
        ConstructorInfo ctor = contextType.GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Length == 10);
        Assert.That(ctor, Is.Not.Null, "NpcMetaContext ctor(10 params) not found.");

        object context = ctor.Invoke(new object[]
        {
            hasRelic,
            hpRatio,
            threatLevel,
            expeditionUrgency,
            nearestRelicDistance,
            nearestReturnZoneDistance,
            nearestShelterDistance,
            nearestTeammateDistance,
            allyInDanger,
            stuckSeconds
        });

        MethodInfo decide = engineType.GetMethod("Decide", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(decide, Is.Not.Null, "NPCMetaDecisionEngine.Decide() not found.");

        object[] args = { context };
        object result = decide.Invoke(engine, args);
        Assert.That(result, Is.Not.Null, "Decide() returned null.");
        return result;
    }

    private static string ReadGoalName(object decision)
    {
        Type decisionType = decision.GetType();
        PropertyInfo goalProp = decisionType.GetProperty("Goal", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(goalProp, Is.Not.Null, "NpcMetaDecision.Goal property not found.");

        object goalValue = goalProp.GetValue(decision);
        Assert.That(goalValue, Is.Not.Null, "NpcMetaDecision.Goal value is null.");
        return goalValue.ToString();
    }

    private static Type ResolveType(string typeName)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
    }
}
