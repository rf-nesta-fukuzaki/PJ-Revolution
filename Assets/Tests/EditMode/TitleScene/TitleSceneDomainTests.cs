using System;
using System.Reflection;
using NUnit.Framework;

public sealed class TitleSceneDomainTests
{
    [Test]
    public void StateMachine_Rejects_InvalidTransition_FromBootToModal()
    {
        Type stateMachineType = GetTypeOrFail("TitleSceneStateMachine, Assembly-CSharp");
        Type triggerType = GetTypeOrFail("TitleSceneTrigger, Assembly-CSharp");

        object stateMachine = Activator.CreateInstance(stateMachineType);
        bool changed = InvokeTryFire(stateMachineType, stateMachine, triggerType, "OpenModal");
        string current = ReadCurrentStateName(stateMachineType, stateMachine);

        Assert.That(changed, Is.False);
        Assert.That(current, Is.EqualTo("Boot"));
    }

    [Test]
    public void StateMachine_Accepts_ValidTransitionPath_ToTransitioning()
    {
        Type stateMachineType = GetTypeOrFail("TitleSceneStateMachine, Assembly-CSharp");
        Type triggerType = GetTypeOrFail("TitleSceneTrigger, Assembly-CSharp");

        object stateMachine = Activator.CreateInstance(stateMachineType);

        Assert.That(InvokeTryFire(stateMachineType, stateMachine, triggerType, "IntroStart"), Is.True);
        Assert.That(InvokeTryFire(stateMachineType, stateMachine, triggerType, "IntroComplete"), Is.True);
        Assert.That(InvokeTryFire(stateMachineType, stateMachine, triggerType, "StartGame"), Is.True);
        Assert.That(ReadCurrentStateName(stateMachineType, stateMachine), Is.EqualTo("Transitioning"));
    }

    [Test]
    public void MenuInteractor_StartGame_WhenSceneExists_ReturnsLoadSceneCommand()
    {
        object stateMachine = CreateReadyStateMachine();
        object interactor = CreateInteractor(stateMachine, "TestScene");

        object command = InvokeHandle(interactor, "StartGame");

        AssertCommand(command, "LoadScene", "TestScene");
        Assert.That(ReadCurrentStateName(stateMachine.GetType(), stateMachine), Is.EqualTo("Transitioning"));
    }

    [Test]
    public void MenuInteractor_StartGame_WhenSceneMissing_ReturnsNoneCommand_AndStaysReady()
    {
        object stateMachine = CreateReadyStateMachine();
        object interactor = CreateInteractor(stateMachine, "MissingScene_ForTitleMenuInteractorTests");

        object command = InvokeHandle(interactor, "StartGame");

        AssertCommand(command, "None", string.Empty);
        Assert.That(ReadCurrentStateName(stateMachine.GetType(), stateMachine), Is.EqualTo("Ready"));
    }

    [TestCase("Settings", "ToggleSettings")]
    [TestCase("Credits", "ToggleCredits")]
    [TestCase("Exit", "Quit")]
    public void MenuInteractor_NonSceneActions_ReturnExpectedCommand(string actionName, string expectedCommandType)
    {
        object stateMachine = CreateReadyStateMachine();
        object interactor = CreateInteractor(stateMachine, "TestScene");

        object command = InvokeHandle(interactor, actionName);

        AssertCommand(command, expectedCommandType, string.Empty);
        Assert.That(ReadCurrentStateName(stateMachine.GetType(), stateMachine), Is.EqualTo("Ready"));
    }

    private static object CreateReadyStateMachine()
    {
        Type stateMachineType = GetTypeOrFail("TitleSceneStateMachine, Assembly-CSharp");
        Type triggerType = GetTypeOrFail("TitleSceneTrigger, Assembly-CSharp");
        object stateMachine = Activator.CreateInstance(stateMachineType);
        InvokeTryFire(stateMachineType, stateMachine, triggerType, "IntroStart");
        InvokeTryFire(stateMachineType, stateMachine, triggerType, "IntroComplete");
        return stateMachine;
    }

    private static object CreateInteractor(object stateMachine, string sceneName)
    {
        Type interactorType = GetTypeOrFail("TitleMenuInteractor, Assembly-CSharp");
        Type navigatorType = GetTypeOrFail("UnityTitleSceneNavigator, Assembly-CSharp");
        object navigator = Activator.CreateInstance(navigatorType);
        return Activator.CreateInstance(interactorType, stateMachine, navigator, sceneName);
    }

    private static object InvokeHandle(object interactor, string actionName)
    {
        Type interactorType = interactor.GetType();
        Type actionType = GetTypeOrFail("TitleMenuAction, Assembly-CSharp");
        MethodInfo handle = interactorType.GetMethod("Handle", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(handle, Is.Not.Null, "Handle method not found on TitleMenuInteractor.");
        object action = Enum.Parse(actionType, actionName, false);
        return handle.Invoke(interactor, new[] { action });
    }

    private static void AssertCommand(object command, string expectedTypeName, string expectedSceneName)
    {
        Assert.That(command, Is.Not.Null, "TitleCommand is null.");

        Type commandType = command.GetType();
        PropertyInfo typeProp = commandType.GetProperty("Type", BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo sceneNameProp = commandType.GetProperty("SceneName", BindingFlags.Instance | BindingFlags.Public);

        Assert.That(typeProp, Is.Not.Null, "TitleCommand.Type was not found.");
        Assert.That(sceneNameProp, Is.Not.Null, "TitleCommand.SceneName was not found.");

        object actualType = typeProp.GetValue(command);
        object actualSceneName = sceneNameProp.GetValue(command);

        Assert.That(actualType?.ToString(), Is.EqualTo(expectedTypeName));
        Assert.That(actualSceneName as string, Is.EqualTo(expectedSceneName));
    }

    private static bool InvokeTryFire(Type stateMachineType, object stateMachine, Type triggerType, string triggerName)
    {
        MethodInfo tryFire = stateMachineType.GetMethod("TryFire", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(tryFire, Is.Not.Null, "TryFire method not found on TitleSceneStateMachine.");
        object trigger = Enum.Parse(triggerType, triggerName, false);
        object result = tryFire.Invoke(stateMachine, new[] { trigger });
        return result is bool value && value;
    }

    private static string ReadCurrentStateName(Type stateMachineType, object stateMachine)
    {
        PropertyInfo currentProperty = stateMachineType.GetProperty("Current", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(currentProperty, Is.Not.Null, "Current property not found on TitleSceneStateMachine.");
        object current = currentProperty.GetValue(stateMachine);
        return current?.ToString() ?? string.Empty;
    }

    private static Type GetTypeOrFail(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName);
        Assert.That(type, Is.Not.Null, $"Type not found: {assemblyQualifiedName}");
        return type;
    }
}
