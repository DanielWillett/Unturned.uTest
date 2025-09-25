//#define REFLECTION_TOOLS_DEBUG
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using uTest.Discovery;

namespace uTest.Module;

internal delegate bool CompiledTest(TestRunParameters parameters, ref int state, ref object? currentTask, Action continuation);

internal static class TestCompiler
{
    private static readonly Type[] Parameters = [ typeof(TestRunParameters), typeof(int).MakeByRefType(), typeof(object).MakeByRefType(), typeof(Action) ];

    internal const int StateBegin = 0;
    internal const int StateSetup = 1;
    internal const int StateInvoke = 2;
    internal const int StateTearDown = 3;
    // used if an excpetion is thrown
    internal const int StateRerunTearDown = 4;
    internal const int StateFinished = 5;

    internal static CompiledTest? CompileTest(TestRunParameters parameters, ILogger logger)
    {
#if REFLECTION_TOOLS_DEBUG
        if (Accessor.Logger is not ReflectionToolsLogger)
        {
            Accessor.Logger = new ReflectionToolsLogger(logger);
        }
#endif

        ref readonly UnturnedTestInstance test = ref parameters.Test;
        DynamicMethod dynMethod = new DynamicMethod(
            test.Uid,
            MethodAttributes.Public | MethodAttributes.Static,
            CallingConventions.Any,
            typeof(bool),
            Parameters,
            test.Type,
            skipVisibility: true
        ) { InitLocals = false };

#if REFLECTION_TOOLS_DEBUG
        IOpCodeEmitter il = dynMethod.AsEmitter(debuggable: true);
#else
        ILGenerator il = dynMethod.GetILGenerator(2048);
#endif
        Type runnerType = test.Type;
        ConstructorInfo? constructor = runnerType.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
        {
            logger.LogError(string.Format(
                Properties.Resources.LogErrorMissingConstructor,
                test.DisplayName,
                test.Test.ManagedType)
            );
            return null;
        }

        LocalBuilder lclRunner = il.DeclareLocal(runnerType);
        LocalBuilder lclContext = il.DeclareLocal(typeof(ITestContext));
        LocalBuilder didAwait = il.DeclareLocal(typeof(bool));

        Label stBegin = il.DefineLabel(),
              stFinishSetup = il.DefineLabel(),
              stInvokeTest = il.DefineLabel(),
              stFinishTearDown = il.DefineLabel(),
              stRerunTearDown = il.DefineLabel();

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, didAwait);

        // switch (state)
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldind_I4);
        il.Emit(OpCodes.Switch, [ stBegin, stFinishSetup, stInvokeTest, stFinishTearDown, stRerunTearDown]);

        // throw new InvalidProgramException()
        il.Emit(OpCodes.Newobj, typeof(InvalidProgramException).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Throw);

        il.MarkLabel(stBegin);

        // state: 0
        // runner = new TRunner();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Dup); // for stfld
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Dup); // for newobj
        il.Emit(OpCodes.Stloc, lclRunner);

        // ITestContext ctx = new TestContext(parameters, runner)
        // parameters.Context = ctx;
        il.Emit(OpCodes.Newobj, TestContext_Ctor);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Stloc, lclContext);
        il.Emit(OpCodes.Stfld, TestRunParameters_Context);

        if (typeof(ITestClassSetup).IsAssignableFrom(runnerType))
        {
            il.Emit(OpCodes.Ldloc, lclRunner);
            il.Emit(OpCodes.Ldloc, lclContext);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, TestRunParameters_Token);
            il.Emit(OpCodes.Callvirt, ITestClassSetup_SetupAsync);
            Await(il, StateSetup, stFinishSetup, ITestClassSetup_SetupAsync.ReturnType, null, out _, didAwait);
            // state: 1

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, TestRunParameters_Context);
            il.Emit(OpCodes.Callvirt, TestContext_Runner_Get);
            il.Emit(OpCodes.Stloc, lclRunner);
        }
        else
        {
            il.MarkLabel(stFinishSetup);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, TestRunParameters_Context);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stfld, TestContext_HasStarted);

        // force garbage collection
        il.Emit(OpCodes.Ldc_I4, GC.MaxGeneration);
        il.Emit(OpCodes.Ldc_I4_S, (byte)GCCollectionMode.Optimized);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Call, GC_Collect4);

        il.BeginExceptionBlock();
        //Label endTry = il.DefineLabel();

        if (!test.Method.IsStatic)
        {
            // push runner
            il.Emit(OpCodes.Ldloc, lclRunner);
        }

        Label rtnTrue = il.DefineLabel();

        ParameterInfo[] methodParameters = test.Method.GetParameters();
        for (int i = 0; i < test.Arguments.Length; ++i)
        {
            // push parameters.Test.Arguments[i]
            ParameterInfo parameter = methodParameters[i];
            if (TryLoadParameter(in test, il, i, parameter))
                continue;

            logger.LogError(string.Format(
                    Properties.Resources.LogErrorMismatchedParameterType,
                    test.Arguments[i]?.ToString() ?? "null",
                    ManagedIdentifier.GetManagedType(parameter.ParameterType),
                    parameter.Name,
                    test.DisplayName)
            );

            return null;
        }

        Label noSignalStartMtd = il.DefineLabel();
        Label hasSignalStartMtd = il.DefineLabel();
        // parameters.SignalStart?.Invoke(parameters)
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, TestRunParameters_SignalStart);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brfalse, noSignalStartMtd);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_S, (int)TestRunStopwatchStage.Execute);
        il.Emit(OpCodes.Callvirt, Action_TestRunParameters_TestRunStopwatchStage_Invoke);
        il.Emit(OpCodes.Br, hasSignalStartMtd);
        il.MarkLabel(noSignalStartMtd);
        il.Emit(OpCodes.Pop);
        il.MarkLabel(hasSignalStartMtd);

        il.Emit(test.Method.IsStatic ? OpCodes.Call : OpCodes.Callvirt, test.Method);
        Await(il, StateInvoke, stInvokeTest, test.Method.ReturnType, rtnTrue, out _, didAwait);
        // state: 2

        il.BeginFinallyBlock();

        Label noSignalEndMtd = il.DefineLabel();
        Label hasSignalEndMtd = il.DefineLabel();
        // if (!(parameters.SignalEnd == null || didAwait))
        //     parameters.SignalEnd.Invoke(parameters)
        il.Emit(OpCodes.Ldloc, didAwait);
        il.Emit(OpCodes.Brtrue, hasSignalEndMtd);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, TestRunParameters_SignalEnd);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brfalse, noSignalEndMtd);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_S, (int)TestRunStopwatchStage.Execute);
        il.Emit(OpCodes.Callvirt, Action_TestRunParameters_TestRunStopwatchStage_Invoke);
        il.Emit(OpCodes.Br, hasSignalEndMtd);
        il.MarkLabel(noSignalEndMtd);
        il.Emit(OpCodes.Pop);
        il.MarkLabel(hasSignalEndMtd);

        il.EndExceptionBlock();

        //il.MarkLabel(endTry);

        // if (state != 2) { return true; }
        //il.Emit(OpCodes.Ldc_I4_S, (byte)StateInvoke);
        //il.Emit(OpCodes.Ldarg_1);
        //il.Emit(OpCodes.Ldind_I4);
        //il.Emit(OpCodes.Bne_Un, rtnTrue);

        il.MarkLabel(stRerunTearDown);
        if (typeof(ITestClassTearDown).IsAssignableFrom(runnerType))
        {
            // runner.TearDownAsync(parameters.Token)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, TestRunParameters_Context);
            il.Emit(OpCodes.Callvirt, TestContext_Runner_Get);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, TestRunParameters_Token);
            il.Emit(OpCodes.Callvirt, ITestClassTearDown_TearDownAsync);
            Await(il, StateTearDown, stFinishTearDown, ITestClassTearDown_TearDownAsync.ReturnType, null, out _, didAwait);
            // state: 3
        }
        else
        {
            il.MarkLabel(stFinishTearDown);
        }

        // state = 4
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldc_I4_S, (byte)StateFinished);
        il.Emit(OpCodes.Stind_I4);

        // return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(rtnTrue);

        // return true
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        return (CompiledTest)dynMethod.CreateDelegate(typeof(CompiledTest));
    }

    private static void Await(
#if REFLECTION_TOOLS_DEBUG
        IOpCodeEmitter il,
#else
        ILGenerator il,
#endif
        int finalState, Label jumpLabel, Type awaitType, Label? tryLabel, out LocalBuilder? result, LocalBuilder boolDidAwait)
    {
        result = null;

        TaskAwaitableHelper.AwaitableInfo info = TaskAwaitableHelper.GetAwaitableInfo(awaitType);
        if (!info.IsValidAwaitable)
        {
            il.MarkLabel(jumpLabel);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_S, (byte)finalState);
            il.Emit(OpCodes.Stind_I4);

            if (awaitType != typeof(void))
            {
                result = il.DeclareLocal(awaitType);
                il.Emit(OpCodes.Stloc, result);
            }

            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, boolDidAwait);
            return;
        }

        LocalBuilder taskLcl = il.DeclareLocal(awaitType);
        il.Emit(OpCodes.Stloc, taskLcl);
        // var task = task.ConfigureAwait(false)
        if (info.ConfigureAwaitMethod != null)
        {
            if (awaitType.IsValueType)
            {
                il.Emit(OpCodes.Ldloca, taskLcl);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Call, info.ConfigureAwaitMethod);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, taskLcl);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, info.ConfigureAwaitMethod);
            }

            if (info.ConfigureAwaitMethod.ReturnType != typeof(void))
            {
                taskLcl = il.DeclareLocal(info.TaskType!);
                il.Emit(OpCodes.Stloc, taskLcl);
            }
        }

        // var lclAwaiter = task.GetAwaiter()
        if (taskLcl.LocalType!.IsValueType)
        {
            il.Emit(OpCodes.Ldloca, taskLcl);
            il.Emit(OpCodes.Call, info.GetAwaiterMethod!);
        }
        else
        {
            il.Emit(OpCodes.Ldloc, taskLcl);
            il.Emit(OpCodes.Callvirt, info.GetAwaiterMethod!);
        }

        Type awaiterType = info.GetAwaiterMethod!.ReturnType;
        LocalBuilder awaiterLcl = il.DeclareLocal(awaiterType);
        il.Emit(OpCodes.Stloc, awaiterLcl);

        // if (!isCompleted) {
        if (awaiterType.IsValueType)
        {
            il.Emit(OpCodes.Ldloca, awaiterLcl);
            il.Emit(OpCodes.Call, info.IsCompletedProperty!.GetMethod!);
        }
        else
        {
            il.Emit(OpCodes.Ldloc, awaiterLcl);
            il.Emit(OpCodes.Callvirt, info.IsCompletedProperty!.GetMethod!);
        }

        Label alreadyCompleted = il.DefineLabel();

        il.Emit(OpCodes.Brtrue, alreadyCompleted);

        // didAwait = true
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stloc, boolDidAwait);

        // (ref currentTask) = awaiter
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldloc, awaiterLcl);
        if (awaiterType.IsValueType)
        {
            il.Emit(OpCodes.Box, awaiterType);
        }
        il.Emit(OpCodes.Stind_Ref);

        // (ref state) = state
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldc_I4_S, (byte)finalState);
        il.Emit(OpCodes.Stind_I4);

        // awaiter.OnCompleted(continuation)
        if (awaiterType.IsValueType)
        {
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Unbox, awaiterType);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Constrained, awaiterType);
            il.Emit(OpCodes.Callvirt, typeof(ICriticalNotifyCompletion).IsAssignableFrom(awaiterType)
                ? ICriticalNotifyCompletion_UnsafeOnCompleted
                : INotifyCompletion_OnCompleted
            );
        }
        else
        {
            il.Emit(OpCodes.Ldloc, awaiterLcl);
            if (typeof(ICriticalNotifyCompletion).IsAssignableFrom(awaiterType))
            {
                il.Emit(OpCodes.Ldarg_3);
                il.Emit(OpCodes.Callvirt, ICriticalNotifyCompletion_UnsafeOnCompleted);
            }
            else
            {
                // if (awaiter is ICriticalNotifyCompletion) {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Isinst, typeof(ICriticalNotifyCompletion));
                Label notCritical = il.DefineLabel(),
                      critical = il.DefineLabel();
                il.Emit(OpCodes.Brfalse, notCritical);

                // awaiter.UnsafeOnCompleted
                il.Emit(OpCodes.Ldarg_3);
                il.Emit(OpCodes.Callvirt, ICriticalNotifyCompletion_UnsafeOnCompleted);
                il.Emit(OpCodes.Br, critical);

                // } else {
                il.MarkLabel(notCritical);
                il.Emit(OpCodes.Ldarg_3);
                il.Emit(OpCodes.Callvirt, INotifyCompletion_OnCompleted);

                il.MarkLabel(critical);
            }
        }

        // return true
        if (!tryLabel.HasValue)
        {
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
        }
        else
        {
            il.Emit(OpCodes.Leave, tryLabel.Value);
        }

        il.MarkLabel(jumpLabel);

        // called on continuation:

        // awaiter = (TAwaiter)currentTask;
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldind_Ref);
        if (awaiterType.IsValueType)
        {
            il.Emit(OpCodes.Unbox, awaiterType);
        }
        else
        {
            il.Emit(OpCodes.Castclass, awaiterType);
        }

        Label skipComplete = il.DefineLabel();
        il.Emit(OpCodes.Br, skipComplete);

        // end called on continuation

        il.MarkLabel(alreadyCompleted);

        // } else /* isCompleted */ {
        il.Emit(awaiterType.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc, awaiterLcl);

        il.MarkLabel(skipComplete);

        // didAwait = false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, boolDidAwait);

        // var result = awaiter.GetResult();
        il.Emit(awaiterType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, info.GetResultMethod!);
        if (info.GetResultMethod!.ReturnType != typeof(void))
        {
            result = il.DeclareLocal(awaitType);
            il.Emit(OpCodes.Stloc, result);
        }

        // }
    }

    private static bool TryLoadParameter(in UnturnedTestInstance test,
#if REFLECTION_TOOLS_DEBUG
        IOpCodeEmitter il,
#else
        ILGenerator il,
#endif
        int index, ParameterInfo parameter)
    {
        Type paramType = parameter.ParameterType;
        ref object? testArgument = ref test.Arguments[index];

        Type? underlyingNullableType = Nullable.GetUnderlyingType(paramType);
        if (testArgument == null)
        {
            if (!paramType.IsValueType)
            {
                il.Emit(OpCodes.Ldnull);
                return true;
            }

            if (underlyingNullableType == null)
            {
                return false;
            }

            // T? val = default(T?);
            LocalBuilder lb = il.DeclareLocal(paramType);
            il.Emit(OpCodes.Ldloca, lb);
            il.Emit(OpCodes.Initobj, paramType);
            il.Emit(OpCodes.Ldloc, lb);
            return true;
        }

        Type valueType = underlyingNullableType ?? paramType;

        if (!valueType.IsInstanceOfType(testArgument))
        {
            try
            {
                testArgument = Convert.ChangeType(testArgument, paramType);
            }
            catch
            {
                return false;
            }
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, TestRunParameters_Test);
        il.Emit(OpCodes.Call, UnturnedTestInstance_Arguments_Get);
        il.Emit(OpCodes.Ldc_I4, index);
        il.Emit(OpCodes.Ldelem_Ref);
        if (paramType.IsValueType)
            il.Emit(OpCodes.Unbox_Any, paramType);

        return true;
    }

    // ReSharper disable InconsistentNaming

    private static readonly ConstructorInfo TestContext_Ctor;
    private static readonly MethodInfo ITestClassSetup_SetupAsync;
    private static readonly MethodInfo ITestClassTearDown_TearDownAsync;
    private static readonly MethodInfo Action_TestRunParameters_TestRunStopwatchStage_Invoke;
    private static readonly FieldInfo TestRunParameters_Token;
    private static readonly FieldInfo TestRunParameters_Context;
    private static readonly FieldInfo TestRunParameters_Test;
    private static readonly FieldInfo TestRunParameters_SignalStart;
    private static readonly FieldInfo TestRunParameters_SignalEnd;
    private static readonly FieldInfo TestContext_HasStarted;

    private static readonly MethodInfo INotifyCompletion_OnCompleted;
    private static readonly MethodInfo ICriticalNotifyCompletion_UnsafeOnCompleted;
    private static readonly MethodInfo GC_Collect4;

    private static readonly MethodInfo UnturnedTestInstance_Arguments_Get;
    private static readonly MethodInfo TestContext_Runner_Get;

    // ReSharper restore InconsistentNaming

    static TestCompiler()
    {
        TestContext_Ctor = typeof(TestContext)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, [ typeof(TestRunParameters), typeof(ITestClass) ], null)
            ?? throw new MissingMethodException(nameof(TestContext), ".ctor");

        ITestClassSetup_SetupAsync = typeof(ITestClassSetup)
            .GetMethod(nameof(ITestClassSetup.SetupAsync), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(nameof(ITestClassSetup), nameof(ITestClassSetup.SetupAsync));

        ITestClassTearDown_TearDownAsync = typeof(ITestClassTearDown)
            .GetMethod(nameof(ITestClassTearDown.TearDownAsync), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(nameof(ITestClassTearDown), nameof(ITestClassTearDown.TearDownAsync));

        GC_Collect4 = typeof(GC)
            .GetMethod(nameof(GC.Collect), BindingFlags.Static | BindingFlags.Public, null, CallingConventions.Any, [ typeof(int), typeof(GCCollectionMode), typeof(bool), typeof(bool) ], null)
            ?? throw new MissingMethodException(nameof(GC), nameof(GC.Collect));

        Action_TestRunParameters_TestRunStopwatchStage_Invoke = typeof(Action<TestRunParameters, TestRunStopwatchStage>)
            .GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException("Action<TestRunParameters, TestRunStopwatchStage>", "Invoke");

        TestRunParameters_Token = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.Token), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.Token));

        TestRunParameters_Context = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.Context), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.Context));

        TestRunParameters_Test = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.Test), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.Test));

        TestRunParameters_SignalStart = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.SignalStart), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.SignalStart));

        TestRunParameters_SignalEnd = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.SignalEnd), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.SignalEnd));

        TestContext_HasStarted = typeof(TestContext)
            .GetField(nameof(TestContext.HasStarted), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestContext), nameof(TestContext.HasStarted));

        INotifyCompletion_OnCompleted = typeof(INotifyCompletion)
            .GetMethod(nameof(INotifyCompletion.OnCompleted), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(INotifyCompletion), nameof(INotifyCompletion.OnCompleted));

        ICriticalNotifyCompletion_UnsafeOnCompleted = typeof(ICriticalNotifyCompletion)
            .GetMethod(nameof(ICriticalNotifyCompletion.UnsafeOnCompleted), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(ICriticalNotifyCompletion), nameof(ICriticalNotifyCompletion.UnsafeOnCompleted));

        UnturnedTestInstance_Arguments_Get = typeof(UnturnedTestInstance)
            .GetProperty(nameof(UnturnedTestInstance.Arguments), BindingFlags.Public | BindingFlags.Instance)?.GetMethod
            ?? throw new MissingMethodException(nameof(UnturnedTestInstance), "get_" + nameof(UnturnedTestInstance.Arguments));

        TestContext_Runner_Get = typeof(TestContext)
            .GetProperty(nameof(TestContext.Runner), BindingFlags.Public | BindingFlags.Instance)?.GetMethod
            ?? throw new MissingMethodException(nameof(TestContext), "get_" + nameof(TestContext.Runner));
    }
}

#if REFLECTION_TOOLS_DEBUG
internal class ReflectionToolsLogger(ILogger logger) : IReflectionToolsLogger
{
    /// <inheritdoc />
    public void LogDebug(string source, string message)
    {
        logger.LogDebug($"[{source}] {message}");
    }

    /// <inheritdoc />
    public void LogInfo(string source, string message)
    {
        logger.LogInformation($"[{source}] {message}");
    }

    /// <inheritdoc />
    public void LogWarning(string source, string message)
    {
        logger.LogWarning($"[{source}] {message}");
    }

    /// <inheritdoc />
    public void LogError(string source, Exception? ex, string? message)
    {
        if (ex != null)
        {
            logger.LogError($"[{source}] {message}", ex);
        }
        else
        {
            logger.LogError($"[{source}] {message}");
        }
    }
}
#endif