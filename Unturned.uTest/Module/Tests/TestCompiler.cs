#define REFLECTION_TOOLS_DEBUG
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using uTest.Compat.DependencyInjection;
using uTest.Discovery;
#if REFLECTION_TOOLS_DEBUG
using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
#endif

namespace uTest.Module;

internal delegate void TestInvoker(TestRunParameters parameters, TestContext context, out object? awaiter, Action continuation);
internal delegate void TestFinalizer(object awaiter);

internal static class TestCompiler
{
    private static readonly Type[] InvokeMethodParameters =
    [
        typeof(TestRunParameters) /* parameters */,
        typeof(TestContext) /* context */,
        typeof(object).MakeByRefType() /* awaiter */,
        typeof(Action) /* continuation */
    ];

    private static readonly Type[] FinalizeMethodParameters =
    [
        typeof(object) /* awaiter */
    ];

    internal static (TestInvoker?, TestFinalizer?) CompileTestMethods(TestRunParameters parameters, ILogger logger)
    {
#if REFLECTION_TOOLS_DEBUG
        if (Accessor.Logger is not ReflectionToolsLogger)
        {
            Accessor.Logger = new ReflectionToolsLogger(logger);
        }
#endif

        ref readonly UnturnedTestInstance test = ref parameters.Test.Instance;

        TaskAwaitableHelper.AwaitableInfo awaitInfo = TaskAwaitableHelper.GetAwaitableInfo(test.Method.ReturnType);

        TestInvoker? compileTestInvoker = CompileTestInvoker(in awaitInfo, in test, logger);

        if (compileTestInvoker == null)
            return (null, null);

        return (
            compileTestInvoker,
            CompileTestFinalizer(in awaitInfo, in test)
        );
    }

    private static TestInvoker? CompileTestInvoker(in TaskAwaitableHelper.AwaitableInfo awaitInfo, in UnturnedTestInstance test, ILogger logger)
    {
        DynamicMethod dynMethod = new DynamicMethod(
                test.Uid + "_Invoke",
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Any,
                typeof(void),
                InvokeMethodParameters,
                test.Type,
                skipVisibility: true
            )
        { InitLocals = false };

#if REFLECTION_TOOLS_DEBUG
        IOpCodeEmitter il = dynMethod.AsEmitter(debuggable: true);
#else
        ILGenerator il = dynMethod.GetILGenerator(2048);
#endif
        bool callvirt = false;
        if (!test.Method.IsStatic)
        {
            // push runner
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, TestContext_Runner_Get);
            if (test.Type.IsValueType)
            {
                il.Emit(OpCodes.Unbox, test.Type);
            }
            else
            {
                callvirt = true;
            }
        }

        ParameterInfo[] methodParameters = test.Method.GetParameters();
        for (int i = 0; i < test.Arguments.Length; ++i)
        {
            // push parameters.Test.Arguments[i]
            ParameterInfo parameter = methodParameters[i];
            if (TryLoadParameter(in test, il, i, parameter))
                continue;

            logger.LogError(
                string.Format(
                    Properties.Resources.LogErrorMismatchedParameterType,
                    test.Arguments[i]?.ToString() ?? "null",
                    ManagedIdentifier.GetManagedType(parameter.ParameterType),
                    parameter.Name,
                    test.DisplayName
                )
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

        il.Emit(callvirt ? OpCodes.Callvirt : OpCodes.Call, test.Method);

        Type awaitedType = test.Method.ReturnType;

        if (!awaitInfo.IsValidAwaitable)
        {
            if (awaitedType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }

#if REFLECTION_TOOLS_DEBUG
            il.Emit(OpCodes.Ldstr, "Test didn't return an awaitable task.");
            il.Emit(OpCodes.Call, new Action<string>(UnturnedLog.info).Method);
#endif
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stind_Ref);
        }
        else
        {
            LocalBuilder taskLcl = il.DeclareLocal(awaitedType);
            il.Emit(OpCodes.Stloc, taskLcl);
            // var task = task.ConfigureAwait(false)
            if (awaitInfo.ConfigureAwaitMethod != null)
            {
                if (awaitedType.IsValueType)
                {
                    il.Emit(OpCodes.Ldloca, taskLcl);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, awaitInfo.ConfigureAwaitMethod);
                }
                else
                {
                    il.Emit(OpCodes.Ldloc, taskLcl);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Callvirt, awaitInfo.ConfigureAwaitMethod);
                }

                if (awaitInfo.ConfigureAwaitMethod.ReturnType != typeof(void))
                {
                    taskLcl = il.DeclareLocal(awaitInfo.TaskType!);
                    il.Emit(OpCodes.Stloc, taskLcl);
                }
            }

            // var lclAwaiter = task.GetAwaiter()
            if (taskLcl.LocalType!.IsValueType)
            {
                il.Emit(OpCodes.Ldloca, taskLcl);
                il.Emit(OpCodes.Call, awaitInfo.GetAwaiterMethod!);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, taskLcl);
                il.Emit(OpCodes.Callvirt, awaitInfo.GetAwaiterMethod!);
            }

            Type awaiterType = awaitInfo.GetAwaiterMethod!.ReturnType;
            LocalBuilder awaiterLcl = il.DeclareLocal(awaiterType);
            il.Emit(OpCodes.Stloc, awaiterLcl);

            // if (!isCompleted) {
            if (awaiterType.IsValueType)
            {
                il.Emit(OpCodes.Ldloca, awaiterLcl);
                il.Emit(OpCodes.Call, awaitInfo.IsCompletedProperty!.GetMethod!);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, awaiterLcl);
                il.Emit(OpCodes.Callvirt, awaitInfo.IsCompletedProperty!.GetMethod!);
            }

            Label alreadyCompleted = il.DefineLabel();

            il.Emit(OpCodes.Brtrue, alreadyCompleted);

#if REFLECTION_TOOLS_DEBUG
            il.Emit(OpCodes.Ldstr, "Test didn't complete instantly, awaiting.");
            il.Emit(OpCodes.Call, new Action<string>(UnturnedLog.info).Method);
#endif

            // (ref awaiter) = lclAwaiter
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldloc, awaiterLcl);
            if (awaiterType.IsValueType)
            {
                il.Emit(OpCodes.Box, awaiterType);
            }
            il.Emit(OpCodes.Stind_Ref);

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

            Label lblAwaited = il.DefineLabel();
            il.Emit(OpCodes.Br, lblAwaited);

            // } else /* isCompleted */ {
            il.MarkLabel(alreadyCompleted);

#if REFLECTION_TOOLS_DEBUG
            il.Emit(OpCodes.Ldstr, "Test task completed instantly.");
            il.Emit(OpCodes.Call, new Action<string>(UnturnedLog.info).Method);
#endif
            il.Emit(awaiterType.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc, awaiterLcl);
            // var result = awaiter.GetResult();
            il.Emit(awaiterType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, awaitInfo.GetResultMethod!);
            if (awaitInfo.GetResultMethod!.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }

            // }

            il.MarkLabel(lblAwaited);

            // } finally {
        }

        il.Emit(OpCodes.Ret);

        return (TestInvoker)dynMethod.CreateDelegate(typeof(TestInvoker));
    }

    private static TestFinalizer? CompileTestFinalizer(in TaskAwaitableHelper.AwaitableInfo awaitInfo, in UnturnedTestInstance test)
    {
        if (!awaitInfo.IsValidAwaitable)
            return null;

        DynamicMethod dynMethod = new DynamicMethod(
            test.Uid + "_Finalize",
            MethodAttributes.Public | MethodAttributes.Static,
            CallingConventions.Any,
            typeof(void),
            FinalizeMethodParameters,
            test.Type,
            skipVisibility: true
        )
        { InitLocals = false };

#if REFLECTION_TOOLS_DEBUG
        IOpCodeEmitter il = dynMethod.AsEmitter(debuggable: true);
#else
        ILGenerator il = dynMethod.GetILGenerator(256);
#endif
        il.Emit(OpCodes.Ldarg_0);
        Type awaiterType = awaitInfo.GetAwaiterMethod!.ReturnType;
        if (awaitInfo.TaskType!.IsValueType)
        {
            il.Emit(OpCodes.Unbox, awaiterType);
            il.Emit(OpCodes.Call, awaitInfo.GetResultMethod!);
        }
        else
        {
            il.Emit(OpCodes.Castclass, awaiterType);
            il.Emit(OpCodes.Callvirt, awaitInfo.GetResultMethod!);
        }

        if (awaitInfo.GetResultMethod!.ReturnType != typeof(void))
        {
            il.Emit(OpCodes.Pop);
        }

        il.Emit(OpCodes.Ret);

        return (TestFinalizer)dynMethod.CreateDelegate(typeof(TestFinalizer));
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
        il.Emit(OpCodes.Ldfld, TestRunParameters_Test);
        il.Emit(OpCodes.Ldflda, UnturnedTestInstanceData_Instance);
        il.Emit(OpCodes.Call, UnturnedTestInstance_Arguments_Get);
        il.Emit(OpCodes.Ldc_I4, index);
        il.Emit(OpCodes.Ldelem_Ref);
        if (paramType.IsValueType)
            il.Emit(OpCodes.Unbox_Any, paramType);

        return true;
    }

    internal static MethodInfo CreateTestRunnerActivatorMethod(Type runnerType)
    {
        return ITestRunnerActivator_CreateTestInstance.MakeGenericMethod(runnerType);
    }

    // ReSharper disable InconsistentNaming

    private static readonly MethodInfo Action_TestRunParameters_TestRunStopwatchStage_Invoke;
    private static readonly FieldInfo TestRunParameters_Test;
    private static readonly FieldInfo TestRunParameters_SignalStart;

    private static readonly MethodInfo INotifyCompletion_OnCompleted;
    private static readonly MethodInfo ICriticalNotifyCompletion_UnsafeOnCompleted;

    private static readonly FieldInfo UnturnedTestInstanceData_Instance;

    private static readonly MethodInfo UnturnedTestInstance_Arguments_Get;
    private static readonly MethodInfo TestContext_Runner_Get;

    private static readonly MethodInfo ITestRunnerActivator_CreateTestInstance;


    // ReSharper restore InconsistentNaming

    static TestCompiler()
    {
        Action_TestRunParameters_TestRunStopwatchStage_Invoke = typeof(Action<TestRunParameters, TestRunStopwatchStage>)
            .GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException("Action<TestRunParameters, TestRunStopwatchStage>", "Invoke");

        TestRunParameters_Test = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.Test), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.Test));

        TestRunParameters_SignalStart = typeof(TestRunParameters)
            .GetField(nameof(TestRunParameters.SignalStart), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(TestRunParameters), nameof(TestRunParameters.SignalStart));

        INotifyCompletion_OnCompleted = typeof(INotifyCompletion)
            .GetMethod(nameof(INotifyCompletion.OnCompleted), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(INotifyCompletion), nameof(INotifyCompletion.OnCompleted));

        ICriticalNotifyCompletion_UnsafeOnCompleted = typeof(ICriticalNotifyCompletion)
            .GetMethod(nameof(ICriticalNotifyCompletion.UnsafeOnCompleted), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(ICriticalNotifyCompletion), nameof(ICriticalNotifyCompletion.UnsafeOnCompleted));

        UnturnedTestInstanceData_Instance = typeof(UnturnedTestInstanceData)
            .GetField(nameof(UnturnedTestInstanceData.Instance), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(UnturnedTestInstanceData), nameof(UnturnedTestInstanceData.Instance));

        UnturnedTestInstance_Arguments_Get = typeof(UnturnedTestInstance)
            .GetProperty(nameof(UnturnedTestInstance.Arguments), BindingFlags.Public | BindingFlags.Instance)?.GetMethod
            ?? throw new MissingMethodException(nameof(UnturnedTestInstance), "get_" + nameof(UnturnedTestInstance.Arguments));

        TestContext_Runner_Get = typeof(TestContext)
            .GetProperty(nameof(TestContext.Runner), BindingFlags.Public | BindingFlags.Instance)?.GetMethod
            ?? throw new MissingMethodException(nameof(TestContext), "get_" + nameof(TestContext.Runner));

        ITestRunnerActivator_CreateTestInstance = typeof(ITestRunnerActivator)
            .GetMethod(nameof(ITestRunnerActivator.CreateTestInstance), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(ITestRunnerActivator), nameof(ITestRunnerActivator.CreateTestInstance));
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