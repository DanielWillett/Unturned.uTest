using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace uTest;

public readonly struct GameThreadTask
{
    private readonly GameThreadTaskAwaiter _awaiter;

    public static GameThreadTask CompletedTask { get; } = new GameThreadTask(true, false);

    internal GameThreadTask(bool isCompleted, bool isSkip, CancellationToken token = default)
    {
        _awaiter = new GameThreadTaskAwaiter(isCompleted, isSkip, token);
    }

    public GameThreadTaskAwaiter GetAwaiter()
    {
        return _awaiter ?? CompletedTask.GetAwaiter();
    }
}

public class GameThreadTaskAwaiter : ICriticalNotifyCompletion
{
    private readonly bool _isSkip;
    private CancellationToken _token;
    private Action? _continuation;
    private ExecutionContext? _executionContext;
    internal ExceptionDispatchInfo? Exception;
    private bool _disposed;
    
    // do not make readonly, NS2.1 has the struct as readonly but NF doesnt
    private CancellationTokenRegistration _tokenCancel;

    public bool IsCompleted { get; private set; }

    ~GameThreadTaskAwaiter()
    {
        Dispose();
    }

    public GameThreadTaskAwaiter(bool shouldStartCompleted, bool isSkip, CancellationToken token)
    {
        IsCompleted = shouldStartCompleted;

        _isSkip = isSkip;
        _token = token;

        if (token.CanBeCanceled)
        {
            _tokenCancel = token.Register(static me =>
            {
                // token will be cancelled
                ((GameThreadTaskAwaiter)me).Complete();
            }, this);
        }
    }

    public void GetResult()
    {
        Exception?.Throw();

        CancellationToken tkn = _token;
        if (tkn.IsCancellationRequested)
            throw new OperationCanceledException(tkn);

        if (!GameThread.IsCurrent)
            throw new InvalidOperationException("Do not use GetResult directly on this task.");
    }

    /// <inheritdoc />
    public void UnsafeOnCompleted(Action continuation)
    {
        OnCompletedIntl(continuation, flowExecutionContext: false);
    }

    /// <inheritdoc />
    public void OnCompleted(Action continuation)
    {
        OnCompletedIntl(continuation, flowExecutionContext: true);
    }

    internal virtual void OnCompletedIntl(Action continuation, bool flowExecutionContext)
    {
        _continuation = continuation;

        if (!_isSkip && GameThread.IsCurrent)
        {
            Complete();
            return;
        }

        if (flowExecutionContext)
            _executionContext = ExecutionContext.Capture();

        GameThread.QueueTask(this);
    }

    internal void Complete()
    {
        if (IsCompleted || _disposed)
            return;

        _tokenCancel.Dispose();
        _token = CancellationToken.None;

        if (_executionContext != null)
        {
            ExecutionContext.Run(_executionContext, static s =>
            {
                CompleteIntl((GameThreadTaskAwaiter)s!);
            }, this);
            _executionContext = null;
        }
        else
        {
            CompleteIntl(this);
        }

        return;
        static void CompleteIntl(GameThreadTaskAwaiter me)
        {
            Action? continuation = me._continuation;
            me.IsCompleted = true;
            if (continuation == null)
                return;

            continuation.Invoke();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _tokenCancel.Dispose();
    }
}