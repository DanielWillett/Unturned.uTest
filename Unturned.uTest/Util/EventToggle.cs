using System;
using System.Diagnostics;

namespace uTest;

internal static class EventToggle
{
    public static bool IsInProgress { [DebuggerStepThrough] get; [DebuggerStepThrough] private set; }

    [DebuggerStepThrough]
    public static void Invoke<T>(T state, Action<T> action)
    {
        GameThread.Assert();

        IsInProgress = true;
        try
        {
            action(state);
        }
        finally
        {
            IsInProgress = false;
        }
    }

    [DebuggerStepThrough]
    public static void Invoke(Action action)
    {
        GameThread.Assert();

        IsInProgress = true;
        try
        {
            action();
        }
        finally
        {
            IsInProgress = false;
        }
    }

    [DebuggerStepThrough]
    public static TOut Invoke<T, TOut>(T state, Func<T, TOut> action)
    {
        GameThread.Assert();

        IsInProgress = true;
        try
        {
            return action(state);
        }
        finally
        {
            IsInProgress = false;
        }
    }

    [DebuggerStepThrough]
    public static TOut Invoke<TOut>(Func<TOut> action)
    {
        GameThread.Assert();

        IsInProgress = true;
        try
        {
            return action();
        }
        finally
        {
            IsInProgress = false;
        }
    }
}
