using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace uTest;

/// <summary>
/// Utility for checking if the current thread is the game thread.
/// </summary>
public static class GameThread
{
    private static readonly List<IRunAndWaitState> Waiters = new List<IRunAndWaitState>();
    private static readonly ConcurrentQueue<Action> Continuations = new ConcurrentQueue<Action>();
    private static readonly ConcurrentQueue<GameThreadTaskAwaiter> TaskAwaiters = new ConcurrentQueue<GameThreadTaskAwaiter>();
    private static bool _hasFlushedRunAndWaits;

    /// <summary>
    /// Whether or not the current thread is the main Unity thread.
    /// </summary>
    [field: ThreadStatic]
    public static bool IsCurrent { get; private set; }

    /// <summary>
    /// Throws an exception if the current thread isn't the game thread.
    /// </summary>
    /// <exception cref="GameThreadException"/>
    public static void Assert()
    {
        if (!IsCurrent)
            throw new GameThreadException();
    }

    /// <summary>
    /// Awaitable method that switches the currently running async method to the game thread until the next context switch.
    /// </summary>
    public static GameThreadTask Switch(CancellationToken token = default)
    {
        return IsCurrent
            ? GameThreadTask.CompletedTask
            : new GameThreadTask(false);
    }

    /// <summary>
    /// Throws an exception if the current thread isn't the game thread if <c>DEBUG</c> is defined.
    /// </summary>
    /// <exception cref="GameThreadException"/>
    [Conditional("DEBUG")]
    public static void AssertConditional()
    {
        if (!IsCurrent)
            throw new GameThreadException();
    }

    /// <summary>
    /// Ran on startup to configure which thread is the main thread (the current one).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static void Setup()
    {
        IsCurrent = true;
    }

    internal static void RunContinuations()
    {
        while (TaskAwaiters.TryDequeue(out GameThreadTaskAwaiter awaiter))
        {
            awaiter.Complete();
        }

        while (Continuations.TryDequeue(out Action action))
        {
            action();
        }
    }

    internal static void FlushRunAndWaits()
    {
        Assert();

        _hasFlushedRunAndWaits = true;

        lock (Waiters)
        {
            foreach (IRunAndWaitState state in Waiters)
            {
                state.Run();
            }

            Waiters.Clear();
        }
    }

    private const int RunAndWaitTimeout = 60000;

    /// <summary>
    /// Run a callback on the game thread.
    /// </summary>
    /// <remarks>Exceptions will be logged.</remarks>
    /// <param name="action">The code to run on the main thread.</param>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    public static void Run(Action action)
    {
        if (IsCurrent)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                CommandWindow.LogError(string.Format(Properties.Resources.RunOnMainThreadError, action.Method.DeclaringType));
                CommandWindow.LogError(ex);
            }

            return;
        }

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunState state = new RunState(action);
        Continuations.Enqueue(state.Run);
    }

    /// <summary>
    /// Run a callback on the game thread.
    /// </summary>
    /// <remarks>Exceptions will be logged.</remarks>
    /// <param name="arg">Generic argument to supply to the action. Helps avoid closure allocations.</param>
    /// <param name="action">The code to run on the main thread.</param>
    /// <typeparam name="T">Generic argument to supply to the action. Helps avoid closure allocations.</typeparam>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    public static void Run<T>(T arg, Action<T> action)
    {
        if (IsCurrent)
        {
            try
            {
                action(arg);
            }
            catch (Exception ex)
            {
                CommandWindow.LogError(string.Format(Properties.Resources.RunOnMainThreadError, action.Method.DeclaringType));
                CommandWindow.LogError(ex);
            }

            return;
        }

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunState<T> state = new RunState<T>(action, arg);
        Continuations.Enqueue(state.Run);
    }

    internal static void QueueTask(GameThreadTaskAwaiter awaiterTask)
    {
        TaskAwaiters.Enqueue(awaiterTask);
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="action">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="OperationCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static void RunAndWait(Action action, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
        {
            action();
            return;
        }

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitActionState state = new RunAndWaitActionState(new AutoResetEvent(false), action, token);
        try
        {
            lock (Waiters)
                Waiters.Add(state);

            Continuations.Enqueue(state.Run);
            if (!state.WaitHandle.WaitOne(RunAndWaitTimeout))
                throw new TimeoutException(Properties.Resources.RunAndWaitTimeoutFailure);
        }
        finally
        {
            if (!state.Disposed)
                state.Dispose();
        }

        state.Exception?.Throw();

        if (state.Cancelled)
            throw new OperationCanceledException(token);
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="arg">Generic argument to supply to the action. Helps avoid closure allocations.</param>
    /// <param name="action">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <typeparam name="T">Generic argument to supply to the action. Helps avoid closure allocations.</typeparam>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="OperationCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static void RunAndWait<T>(T arg, Action<T> action, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
        {
            action(arg);
            return;
        }

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitActionState<T> state = new RunAndWaitActionState<T>(new AutoResetEvent(false), action, arg, token);
        try
        {
            lock (Waiters)
                Waiters.Add(state);

            Continuations.Enqueue(state.Run);
            if (!state.WaitHandle.WaitOne(RunAndWaitTimeout))
                throw new TimeoutException(Properties.Resources.RunAndWaitTimeoutFailure);
        }
        finally
        {
            if (!state.Disposed)
                state.Dispose();
        }

        state.Exception?.Throw();

        if (state.Cancelled)
            throw new OperationCanceledException(token);
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete, bringing over it's return value.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="func">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <typeparam name="TOut">Return value of <paramref name="func"/>.</typeparam>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="OperationCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static TOut RunAndWait<TOut>(Func<TOut> func, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
            return func();

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitFuncState<TOut> state = new RunAndWaitFuncState<TOut>(new AutoResetEvent(false), func, token);
        try
        {
            lock (Waiters)
                Waiters.Add(state);

            Continuations.Enqueue(state.Run);
            if (!state.WaitHandle.WaitOne(RunAndWaitTimeout))
                throw new TimeoutException(Properties.Resources.RunAndWaitTimeoutFailure);
        }
        finally
        {
            if (!state.Disposed)
                state.Dispose();
        }

        state.Exception?.Throw();

        if (state.Cancelled)
            throw new OperationCanceledException(token);

        return state.ReturnValue!;
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete, bringing over it's return value.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="arg">Generic argument to supply to the action. Helps avoid closure allocations.</param>
    /// <param name="func">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <typeparam name="T">Generic argument to supply to the action. Helps avoid closure allocations.</typeparam>
    /// <typeparam name="TOut">Return value of <paramref name="func"/>.</typeparam>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="OperationCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static TOut RunAndWait<T, TOut>(T arg, Func<T, TOut> func, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
            return func(arg);

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitFuncState<T, TOut> state = new RunAndWaitFuncState<T, TOut>(new AutoResetEvent(false), func, arg, token);
        try
        {
            lock (Waiters)
                Waiters.Add(state);

            Continuations.Enqueue(state.Run);
            if (!state.WaitHandle.WaitOne(RunAndWaitTimeout))
                throw new TimeoutException(Properties.Resources.RunAndWaitTimeoutFailure);
        }
        finally
        {
            if (!state.Disposed)
                state.Dispose();
        }

        state.Exception?.Throw();

        if (state.Cancelled)
            throw new OperationCanceledException(token);

        return state.ReturnValue!;
    }

    private const TaskCreationOptions RunAndWaitAsyncOptions = TaskCreationOptions.None;

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="action">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="TaskCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static Task RunAndWaitAsync(Action action, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
        {
            action();
            return Task.CompletedTask;
        }

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitAsyncActionState state = new RunAndWaitAsyncActionState(new TaskCompletionSource<object?>(RunAndWaitAsyncOptions), action, token);
        lock (Waiters)
            Waiters.Add(state);

        Continuations.Enqueue(state.Run);
        return state.WaitHandle.Task;
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="arg">Generic argument to supply to the action. Helps avoid closure allocations.</param>
    /// <param name="action">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <typeparam name="T">Generic argument to supply to the action. Helps avoid closure allocations.</typeparam>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="TaskCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static Task RunAndWaitAsync<T>(T arg, Action<T> action, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
        {
            action(arg);
            return Task.CompletedTask;
        }

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitAsyncActionState<T> state = new RunAndWaitAsyncActionState<T>(new TaskCompletionSource<object?>(RunAndWaitAsyncOptions), action, arg, token);
        lock (Waiters)
            Waiters.Add(state);

        Continuations.Enqueue(state.Run);
        return state.WaitHandle.Task;
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete, bringing over it's return value.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="func">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <typeparam name="TOut">Return value of <paramref name="func"/>.</typeparam>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="TaskCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static Task<TOut> RunAndWaitAsync<TOut>(Func<TOut> func, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
            return Task.FromResult(func());

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitAsyncFuncState<TOut> state = new RunAndWaitAsyncFuncState<TOut>(new TaskCompletionSource<TOut>(RunAndWaitAsyncOptions), func, token);
        lock (Waiters)
            Waiters.Add(state);

        Continuations.Enqueue(state.Run);
        return state.WaitHandle.Task;
    }

    /// <summary>
    /// Run a callback on the game thread and wait for it to complete, bringing over it's return value.
    /// </summary>
    /// <remarks>Exceptions will be rethrown.</remarks>
    /// <param name="arg">Generic argument to supply to the action. Helps avoid closure allocations.</param>
    /// <param name="func">The code to run on the main thread.</param>
    /// <param name="token">Cancellation token used to cancel the wait.</param>
    /// <typeparam name="T">Generic argument to supply to the action. Helps avoid closure allocations.</typeparam>
    /// <typeparam name="TOut">Return value of <paramref name="func"/>.</typeparam>
    /// <exception cref="TimeoutException">The next Update loop took more than a minute.</exception>
    /// <exception cref="InvalidOperationException">The game has started shutting down.</exception>
    /// <exception cref="TaskCanceledException">Cancelled by <paramref name="token"/>.</exception>
    public static Task<TOut> RunAndWaitAsync<T, TOut>(T arg, Func<T, TOut> func, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (IsCurrent)
            return Task.FromResult(func(arg));

        if (_hasFlushedRunAndWaits)
            throw new InvalidOperationException(Properties.Resources.RunAndWaitShuttingDown);

        RunAndWaitAsyncFuncState<T, TOut> state = new RunAndWaitAsyncFuncState<T, TOut>(new TaskCompletionSource<TOut>(RunAndWaitAsyncOptions), func, arg, token);
        lock (Waiters)
            Waiters.Add(state);

        Continuations.Enqueue(state.Run);
        return state.WaitHandle.Task;
    }

    private static void RemoveWaiter(IRunAndWaitState waiter)
    {
        lock (Waiters)
        {
            int index = Waiters.IndexOf(waiter);
            if (index < 0)
                return;

            int last = Waiters.Count - 1;
            if (index == last)
            {
                Waiters.RemoveAt(index);
                return;
            }

            Waiters[index] = Waiters[last];
            Waiters.RemoveAt(last);
        }
    }

    #region RunAndWaitStates

    private interface IRunAndWaitState
    {
        void Run();
    }

    private sealed class RunState(Action action)
    {
        private readonly Action _action = action;

        public void Run()
        {
            try
            {
                _action();
            }
            catch (Exception ex)
            {
                CommandWindow.LogError(string.Format(Properties.Resources.RunOnMainThreadError, _action.Method.DeclaringType));
                CommandWindow.LogError(ex);
            }
        }
    }

    private sealed class RunState<T>(Action<T> action, T state)
    {
        private readonly Action<T> _action = action;
        private readonly T _state = state;

        public void Run()
        {
            try
            {
                _action(_state);
            }
            catch (Exception ex)
            {
                CommandWindow.LogError(string.Format(Properties.Resources.RunOnMainThreadError, _action.Method.DeclaringType));
                CommandWindow.LogError(ex);
            }
        }
    }

    private abstract class RunAndWaitBaseState : IRunAndWaitState
    {
        private CancellationTokenRegistration _tokenReg;
        public readonly AutoResetEvent WaitHandle;
        public ExceptionDispatchInfo? Exception;
        public bool Disposed;
        public bool Cancelled;
        protected bool HasRan;

        protected RunAndWaitBaseState(AutoResetEvent waitHandle, CancellationToken token)
        {
            WaitHandle = waitHandle;

            if (token.CanBeCanceled)
                _tokenReg = RegisterWithoutCaptureExecutionContext(token, static state => ((RunAndWaitBaseState)state).Cancel(), this);
        }

        private void Cancel()
        {
            if (HasRan)
                return;
            HasRan = true;
            Cancelled = true;
            WaitHandle.Set();
        }

        public void Dispose()
        {
            Disposed = true;
            WaitHandle.Dispose();
            _tokenReg.Dispose();
            _tokenReg = default;
            RemoveWaiter(this);
        }

        public abstract void Run();
    }

    private abstract class RunAndWaitAsyncBaseState : IRunAndWaitState
    {
        private CancellationTokenRegistration _tokenReg;
        protected bool HasRan;
        protected readonly CancellationToken Token;

        protected RunAndWaitAsyncBaseState(CancellationToken token)
        {
            Token = token;
            if (token.CanBeCanceled)
                _tokenReg = RegisterWithoutCaptureExecutionContext(token, static state => ((RunAndWaitAsyncBaseState)state).Cancel(), this);
        }

        private void Cancel()
        {
            if (HasRan)
                return;
            HasRan = true;
            DoCancel();
            Dispose();
        }

        protected abstract void DoCancel();

        protected void Dispose()
        {
            _tokenReg.Dispose();
            _tokenReg = default;
            RemoveWaiter(this);
        }

        public abstract void Run();
    }

    private sealed class RunAndWaitActionState(AutoResetEvent waitHandle, Action action, CancellationToken token) : RunAndWaitBaseState(waitHandle, token), IRunAndWaitState
    {
        private readonly Action _action = action;

        public override void Run()
        {
            if (HasRan)
                return;
            HasRan = true;
            try
            {
                _action();
            }
            catch (Exception ex)
            {
                Exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                Cancelled = false;
                WaitHandle.Set();
            }
        }
    }

    private sealed class RunAndWaitActionState<T>(AutoResetEvent waitHandle, Action<T> action, T state, CancellationToken token) : RunAndWaitBaseState(waitHandle, token), IRunAndWaitState
    {
        private readonly Action<T> _action = action;
        private readonly T _state = state;

        public override void Run()
        {
            if (HasRan)
                return;
            HasRan = true;
            try
            {
                _action(_state);
            }
            catch (Exception ex)
            {
                Exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                WaitHandle.Set();
            }
        }
    }

    private sealed class RunAndWaitFuncState<TOut>(AutoResetEvent waitHandle, Func<TOut> func, CancellationToken token) : RunAndWaitBaseState(waitHandle, token), IRunAndWaitState
    {
        private readonly Func<TOut> _func = func;
        public TOut? ReturnValue;

        public override void Run()
        {
            if (HasRan)
                return;
            HasRan = true;
            try
            {
                ReturnValue = _func();
            }
            catch (Exception ex)
            {
                Exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                WaitHandle.Set();
            }
        }
    }

    private sealed class RunAndWaitFuncState<T, TOut>(AutoResetEvent waitHandle, Func<T, TOut> func, T state, CancellationToken token) : RunAndWaitBaseState(waitHandle, token), IRunAndWaitState
    {
        private readonly Func<T, TOut> _func = func;
        private readonly T _state = state;
        public TOut? ReturnValue;

        public override void Run()
        {
            if (HasRan)
                return;
            HasRan = true;
            try
            {
                ReturnValue = _func(_state);
            }
            catch (Exception ex)
            {
                Exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class RunAndWaitAsyncActionState(TaskCompletionSource<object?> waitHandle, Action action, CancellationToken token) : RunAndWaitAsyncBaseState(token), IRunAndWaitState
    {
        private readonly Action _action = action;
        private bool _hasRan;

        public readonly TaskCompletionSource<object?> WaitHandle = waitHandle;

        protected override void DoCancel()
        {
            WaitHandle.TrySetCanceled(Token);
        }

        public override void Run()
        {
            if (_hasRan)
                return;
            _hasRan = true;
            try
            {
                _action();
                WaitHandle.SetResult(null);
            }
            catch (Exception ex)
            {
                WaitHandle.SetException(ex);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class RunAndWaitAsyncActionState<T>(TaskCompletionSource<object?> waitHandle, Action<T> action, T state, CancellationToken token) : RunAndWaitAsyncBaseState(token), IRunAndWaitState
    {
        private readonly Action<T> _action = action;
        private readonly T _state = state;
        private bool _hasRan;

        public readonly TaskCompletionSource<object?> WaitHandle = waitHandle;

        protected override void DoCancel()
        {
            WaitHandle.TrySetCanceled(Token);
        }

        public override void Run()
        {
            if (_hasRan)
                return;
            _hasRan = true;
            try
            {
                _action(_state);
                WaitHandle.SetResult(null);
            }
            catch (Exception ex)
            {
                WaitHandle.SetException(ex);
            }
            finally
            {
                RemoveWaiter(this);
            }
        }
    }

    private sealed class RunAndWaitAsyncFuncState<TOut>(TaskCompletionSource<TOut> waitHandle, Func<TOut> func, CancellationToken token) : RunAndWaitAsyncBaseState(token), IRunAndWaitState
    {
        private readonly Func<TOut> _func = func;
        private bool _hasRan;

        public readonly TaskCompletionSource<TOut> WaitHandle = waitHandle;

        protected override void DoCancel()
        {
            WaitHandle.TrySetCanceled(Token);
        }

        public override void Run()
        {
            if (_hasRan)
                return;
            _hasRan = true;
            try
            {
                WaitHandle.SetResult(_func());
            }
            catch (Exception ex)
            {
                WaitHandle.SetException(ex);
            }
            finally
            {
                RemoveWaiter(this);
            }
        }
    }

    private sealed class RunAndWaitAsyncFuncState<T, TOut>(TaskCompletionSource<TOut> waitHandle, Func<T, TOut> func, T state, CancellationToken token) : RunAndWaitAsyncBaseState(token), IRunAndWaitState
    {
        private readonly Func<T, TOut> _func = func;
        private readonly T _state = state;
        private bool _hasRan;

        public readonly TaskCompletionSource<TOut> WaitHandle = waitHandle;

        protected override void DoCancel()
        {
            WaitHandle.TrySetCanceled(Token);
        }

        public override void Run()
        {
            if (_hasRan)
                return;
            _hasRan = true;
            try
            {
                WaitHandle.SetResult(_func(_state));
            }
            catch (Exception ex)
            {
                WaitHandle.SetException(ex);
            }
            finally
            {
                RemoveWaiter(this);
            }
        }
    }

    #endregion

    private static CancellationTokenRegistration RegisterWithoutCaptureExecutionContext(CancellationToken token, Action<object> run, object state)
    {
        bool wasSupressed = true;
        if (!ExecutionContext.IsFlowSuppressed())
        {
            ExecutionContext.SuppressFlow();
            wasSupressed = false;
        }

        try
        {
            return token.Register(run, state, false);
        }
        finally
        {
            if (!wasSupressed)
            {
                ExecutionContext.RestoreFlow();
            }
        }
    }

}

/// <summary>
/// Thrown when a feature that requires game thread access is used from a thread other than the game thread.
/// </summary>
/// <remarks>Thrown by <see cref="GameThread.Assert"/>.</remarks>
public class GameThreadException : InvalidOperationException
{
    /// <summary>
    /// Create a new <see cref="GameThreadException"/> with the default message.
    /// </summary>
    public GameThreadException() : base(Properties.Resources.GameThreadExceptionDefault) { }
}