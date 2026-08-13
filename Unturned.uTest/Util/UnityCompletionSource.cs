using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace uTest;

/// <summary>
/// Replacement for <see cref="TaskCompletionSource{TResult}"/> that dequeues on the main thread (and doesn't store a result).
/// </summary>
public abstract class UnityCompletionSource : IDisposable
{
    private readonly bool _continueInstantly;
    private readonly bool _runCallbackOnMainThread;
    protected readonly TimeSpan CompleteTimeout;
    protected int State;
    protected ExceptionDispatchInfo? Exception;

    private Timer? _timer;

    public UnityCompletionSourceAwaiter Task => (UnityCompletionSourceAwaiter)this;

    private protected UnityCompletionSource(bool continueInstantly, bool runCallbackOnMainThread, TimeSpan timeout)
    {
        _continueInstantly = continueInstantly;
        _runCallbackOnMainThread = runCallbackOnMainThread;
        CompleteTimeout = timeout;
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        _timer = new Timer(HandleTimeout, this, timeout, Timeout.InfiniteTimeSpan);
    }

    private static void HandleTimeout(object state)
    {
        UnityCompletionSource src = (UnityCompletionSource)state;
        Interlocked.Exchange(ref src._timer, null)?.Dispose();
        if (Interlocked.CompareExchange(ref src.State, 4, 0) == 0)
        {
            src.TriggerComplete();
        }
    }

    /// <summary>
    /// Create a new <see cref="UnityCompletionSource"/> to await later.
    /// </summary>
    /// <param name="continueInstantly">
    /// Whether or not to invoke the continuation as soon as a complete state is met, instead of waiting until the next update.
    /// When enabled, this can cause an interesting 'issue' with <see cref="TryComplete"/> where the callback will execute before the function returns.
    /// </param>
    /// <param name="runCallbackOnMainThread">
    /// Whether or not to continue on the main thread after the task is awaited. Equivalent to calling <see cref="GameThread.Switch"/> after awiating the task.
    /// </param>
    [MustDisposeResource]
    public static UnityCompletionSource Create(bool continueInstantly = true, bool runCallbackOnMainThread = true, TimeSpan timeout = default)
    {
        if (timeout.Ticks <= 0)
        {
            timeout = Timeout.InfiniteTimeSpan;
        }

        return new UnityCompletionSourceAwaiter(continueInstantly, runCallbackOnMainThread, timeout);
    }

    private void TriggerComplete()
    {
        bool appropriateThread = !_runCallbackOnMainThread || GameThread.IsCurrent;
        if (appropriateThread && _continueInstantly)
        {
            Task.Complete();
        }
        else
        {
            GameThread.Run(Task.Complete, forceQueue: !_continueInstantly);
        }
    }

    /// <summary>
    /// Signal this <see cref="UnityCompletionSource"/> as completed successfully.
    /// </summary>
    /// <returns><see langword="true"/> if it was signaled correctly, or <see langword="false"/> if it's already been signaled.</returns>
    public bool TryComplete()
    {
        if (Interlocked.CompareExchange(ref State, 1, 0) != 0)
            return false;

        TriggerComplete();
        return true;
    }

    /// <summary>
    /// Signal this <see cref="UnityCompletionSource"/> as cancelled, throwing an <see cref="OperationCanceledException"/> when awaited.
    /// </summary>
    /// <returns><see langword="true"/> if it was signaled correctly, or <see langword="false"/> if it's already been signaled.</returns>
    public bool TryCancel()
    {
        if (Interlocked.CompareExchange(ref State, 2, 0) != 0)
            return false;

        TriggerComplete();
        return true;
    }

    /// <summary>
    /// Signal this <see cref="UnityCompletionSource"/> as faulted, throwing the given <paramref name="exception"/> when awaited.
    /// </summary>
    /// <returns><see langword="true"/> if it was signaled correctly, or <see langword="false"/> if it's already been signaled.</returns>
    public bool TrySetException(Exception exception)
    {
        if (Interlocked.CompareExchange(ref State, 3, 0) != 0)
            return false;

        Exception = ExceptionDispatchInfo.Capture(exception);
        TriggerComplete();
        return true;
    }
    
    /// <summary>
    /// Dispose any resources used by this <see cref="UnityCompletionSource"/>.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _timer, null)?.Dispose();
    }
}

/// <summary>
/// Awaiter type for <see cref="UnityCompletionSource"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UnityCompletionSourceAwaiter : UnityCompletionSource, ICriticalNotifyCompletion
{
    private Action? _continuation;
    private ExecutionContext? _executionContext;

    /// <summary>
    /// Whether or not this <see cref="UnityCompletionSource"/> has been signaled.
    /// </summary>
    public bool IsCompleted => State != 0;

    internal UnityCompletionSourceAwaiter(bool continueInstantly, bool runCallbackOnMainThread, TimeSpan timeout)
        : base(continueInstantly, runCallbackOnMainThread, timeout) { }

    /// <summary>Shouldn't be called by user code.</summary>
    public UnityCompletionSourceAwaiter GetAwaiter()
    {
        return this;
    }

    /// <summary>Shouldn't be called by user code.</summary>
    public void GetResult()
    {
        switch (State)
        {
            case 0: // not complete
                throw new InvalidOperationException(Properties.Resources.InvalidOperationException_TaskGetResultNotSupported);

            case 1: // set
                return;

            case 2: // cancelled
                throw new OperationCanceledException(Properties.Resources.UnityCompletionSourceCancelled);

            case 3: // exception
                Exception?.Throw();
                goto case 2;
            
            default: // case 4: timeout
                throw new TimeoutException(string.Format(Properties.Resources.UnityCompletionSourceTimeout, CompleteTimeout));
        }
    }

    internal void Complete()
    {
        Interlocked.Exchange(ref _continuation, null)?.Invoke();

        if (_executionContext != null)
        {
            ExecutionContext.Run(_executionContext, static s =>
            {
                CompleteIntl((UnityCompletionSourceAwaiter)s!);
            }, this);
            _executionContext = null;
        }
        else
        {
            CompleteIntl(this);
        }

        return;

        static void CompleteIntl(UnityCompletionSourceAwaiter me)
        {
            Interlocked.Exchange(ref me._continuation, null)?.Invoke();
        }
    }

    /// <summary>Shouldn't be called by user code.</summary>
    public void OnCompleted(Action continuation)
    {
        OnCompleted(continuation, false);
    }

    /// <summary>Shouldn't be called by user code.</summary>
    public void UnsafeOnCompleted(Action continuation)
    {
        OnCompleted(continuation, true);
    }

    private void OnCompleted(Action continuation, bool flowExecutionContext)
    {
        if (flowExecutionContext)
            _executionContext = ExecutionContext.Capture();

        _continuation = continuation;
        if (IsCompleted)
        {
            Interlocked.Exchange(ref _continuation, null)?.Invoke();
        }
    }
}