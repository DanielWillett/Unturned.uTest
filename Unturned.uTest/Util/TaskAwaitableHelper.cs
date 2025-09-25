using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace uTest;

/// <summary>
/// Utilities for running methods returning any kinds of awaitable types (except for types that rely on extension methods).
/// </summary>
internal static class TaskAwaitableHelper
{
    private static readonly ConcurrentDictionary<Type, AwaitableInfo> AwaitableCache =
        new ConcurrentDictionary<Type, AwaitableInfo>();

    private static readonly Type[] ConfigureAwaitTypeArgs = [ typeof(bool) ];
    private static readonly object[] ConfigureAwaitArgs = [ false ];

    /// <summary>
    /// Creates an awaitable task from any awaitable return type.
    /// </summary>
    /// <remarks>Doesn't support extension methods.</remarks>
    public static Task<object?> CreateTaskFromReturnValue(object? obj)
    {
        switch (obj)
        {
            case null:
                return Task.FromResult<object?>(null);

            case Task<object?> t:
                return t;
        }

        Type returnType = obj.GetType();

        AwaitableInfo info = GetAwaitableInfo(returnType);

        if (!info.IsValidAwaitable)
        {
            return Task.FromResult<object?>(obj);
        }
        
        object task = obj;
        if (info.ConfigureAwaitMethod != null)
        {
            if (info.ConfigureAwaitMethod.ReturnType == typeof(void))
                info.ConfigureAwaitMethod.Invoke(task, ConfigureAwaitArgs);
            else
                task = info.ConfigureAwaitMethod.Invoke(task, ConfigureAwaitArgs);
        }

        INotifyCompletion? awaiter = info.GetAwaiterMethod!.Invoke(task, Array.Empty<object>()) as INotifyCompletion;
        if (awaiter == null)
        {
            return Task.FromResult<object?>(null);
        }

        // (awaiter.IsCompleted)
        if ((bool)info.IsCompletedProperty!.GetValue(awaiter))
        {
            // return awaiter.GetResult()
            try
            {
                object result = info.GetResultMethod!.Invoke(awaiter, Array.Empty<object>());
                return Task.FromResult<object?>(result);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        TaskCompletionSource<object?> taskSource = new TaskCompletionSource<object?>();

        ExecutionContext? context = null;
        Action callback = () =>
        {
            if (context != null)
            {
                MethodInfo getResultMethod = info.GetResultMethod!;
                ExecutionContext.Run(
                    context,
                    _ => Invoke(getResultMethod, awaiter, taskSource),
                    null
                );
            }
            else
            {
                Invoke(info.GetResultMethod!, awaiter, taskSource);
            }
        };

        if (awaiter is ICriticalNotifyCompletion unsafeNotifyCompletion)
        {
            unsafeNotifyCompletion.UnsafeOnCompleted(callback);
        }
        else
        {
            if (!ExecutionContext.IsFlowSuppressed())
            {
                context = ExecutionContext.Capture();
            }
            awaiter.OnCompleted(callback);
        }

        return taskSource.Task;
    }

    public static AwaitableInfo GetAwaitableInfo(Type returnType)
    {
        return AwaitableCache.GetOrAdd(returnType, returnType =>
        {
            AwaitableInfo info = default;
            info.IsValidAwaitable = true;
            try
            {
                info.ConfigureAwaitMethod = returnType.GetMethod(
                    "ConfigureAwait",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    ConfigureAwaitTypeArgs,
                    null
                );
                ParameterInfo[]? p = info.ConfigureAwaitMethod?.GetParameters();
                if (p != null)
                {
                    if (p.Length != 1 || p[0].ParameterType != typeof(bool))
                        info.ConfigureAwaitMethod = null;
                }
            }
            catch (AmbiguousMatchException)
            {
                info.ConfigureAwaitMethod = returnType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => string.Equals(x.Name, "ConfigureAwait", StringComparison.Ordinal)
                                         && x.GetGenericArguments().Length == 0
                                         && x.GetParameters() is { Length: 1 } p
                                         && p[0].ParameterType == typeof(bool)
                                    );
            }

            info.TaskType = returnType;
            if (info.ConfigureAwaitMethod != null && info.ConfigureAwaitMethod.ReturnType != typeof(void))
            {
                info.TaskType = info.ConfigureAwaitMethod.ReturnType;
            }

            tryAgainWithoutConfAwaitTask:
            try
            {
                info.GetAwaiterMethod = info.TaskType.GetMethod(
                    "GetAwaiter",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null
                );
            }
            catch (AmbiguousMatchException)
            {
                info.GetAwaiterMethod = info.TaskType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => string.Equals(x.Name, "GetAwaiter", StringComparison.Ordinal)
                                         && x.GetGenericArguments().Length == 0
                                         && x.GetParameters().Length == 0
                                    );
            }

            Type? awaiterType = info.GetAwaiterMethod?.ReturnType;
            if (awaiterType == null
                || awaiterType == typeof(void)
                || !typeof(INotifyCompletion).IsAssignableFrom(awaiterType))
            {
                if (info.TryReset(returnType))
                    goto tryAgainWithoutConfAwaitTask;

                return AwaitableInfo.Invalid;
            }

            try
            {
                info.GetResultMethod = awaiterType.GetMethod(
                    "GetResult",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null
                );
            }
            catch (AmbiguousMatchException)
            {
                info.GetResultMethod = awaiterType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => string.Equals(x.Name, "GetResult", StringComparison.Ordinal)
                                         && x.GetGenericArguments().Length == 0
                                         && x.GetParameters().Length == 0
                                    );
            }

            try
            {
                info.IsCompletedProperty = awaiterType.GetProperty(
                    "IsCompleted",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    typeof(bool),
                    Type.EmptyTypes,
                    null
                );
            }
            catch (AmbiguousMatchException)
            {
                info.IsCompletedProperty = awaiterType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => string.Equals(x.Name, "IsCompleted", StringComparison.Ordinal)
                                         && x.GetIndexParameters().Length == 0
                                         && x.PropertyType == typeof(bool)
                                         && x.CanRead
                                    );
            }

            if (info.IsCompletedProperty == null
                || info.GetResultMethod == null
                || !info.IsCompletedProperty.CanRead)
            {
                if (info.TryReset(returnType))
                    goto tryAgainWithoutConfAwaitTask;

                return AwaitableInfo.Invalid;
            }

            return info;
        });
    }

    private static void Invoke(MethodInfo getResultMethod, object awaiter, TaskCompletionSource<object?> taskSource)
    {
        try
        {
            object? result = getResultMethod.Invoke(awaiter, Array.Empty<object>());
            taskSource.TrySetResult(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            taskSource.TrySetException(ex.InnerException);
        }
        catch (Exception ex)
        {
            taskSource.TrySetException(ex);
        }
    }

    public struct AwaitableInfo
    {
        public static readonly AwaitableInfo Invalid = default;

        public bool IsValidAwaitable;
        public MethodInfo? ConfigureAwaitMethod;
        public PropertyInfo? IsCompletedProperty;
        public MethodInfo? GetResultMethod;
        public MethodInfo? GetAwaiterMethod;
        public Type? TaskType;

        internal bool TryReset(Type returnType)
        {
            if (returnType == TaskType)
                return false;

            TaskType = returnType;
            ConfigureAwaitMethod = null;
            return true;
        }
    }
}
