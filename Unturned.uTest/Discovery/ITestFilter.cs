using System;
using System.Linq;

namespace uTest.Discovery;

internal interface ITestFilter
{
    TestFilterType Type { get; }

    #region UidList

    /// <exception cref="InvalidOperationException"/>
    int UidCount { get; }

    /// <exception cref="InvalidOperationException"/>
    Task ForEachUid<TState>(ref TState state, StateAsyncAction<TState, string> action);

    /// <exception cref="InvalidOperationException"/>
    Task ForEachUid<TState>(ref TState state, StateBreakableAsyncAction<TState, string> action);

    /// <exception cref="InvalidOperationException"/>
    void ForEachUid<TState>(ref TState state, StateAction<TState, string> action);

    /// <exception cref="InvalidOperationException"/>
    void ForEachUid<TState>(ref TState state, StateBreakableAction<TState, string> action);

    #endregion

    #region TreePath

    /// <exception cref="InvalidOperationException"/>
    string TreePath { get; }

    /// <exception cref="InvalidOperationException"/>
    bool MatchesTreePathFilter(in UnturnedTestInstance instance);

    /// <exception cref="InvalidOperationException"/>
    bool MatchesTreePathFilter(in UnturnedTestInstance instance, bool needsProperties);

    #endregion
}

internal delegate ValueTask StateAsyncAction<TState, in TArg>(ref TState state, TArg arg);

/// <returns>Should continue?</returns>
internal delegate ValueTask<bool> StateBreakableAsyncAction<TState, in TArg>(ref TState state, TArg arg);


internal delegate void StateAction<TState, in TArg>(ref TState state, TArg arg);

/// <returns>Should continue?</returns>
internal delegate bool StateBreakableAction<TState, in TArg>(ref TState state, TArg arg);

internal enum TestFilterType
{
    TreePath,
    UidList
}

internal sealed class UidListFilter : ITestFilter
{
    private readonly string[] _uids;

    public UidListFilter(string[] uids)
    {
        _uids = uids;
    }

    public TestFilterType Type => TestFilterType.UidList;
    public int UidCount => _uids.Length;

    public Task ForEachUid<TState>(ref TState state, StateAsyncAction<TState, string> action)
    {
        Task[] tasks = new Task[_uids.Length];
        Task completedTask = Task.CompletedTask;
        for (int i = 0; i < _uids.Length; i++)
        {
            string uid = _uids[i];
            ValueTask vt = action(ref state, uid);
            if (vt.IsCompleted)
                tasks[i] = completedTask;
            else
                tasks[i] = vt.AsTask();
        }

        return Task.WhenAll(tasks);
    }

    public Task ForEachUid<TState>(ref TState state, StateBreakableAsyncAction<TState, string> action)
    {
        Task[] tasks = new Task[_uids.Length];
        int i = 0;
        Task completedTask = Task.CompletedTask;
        for (; i < _uids.Length; i++)
        {
            string uid = _uids[i];
            ValueTask<bool> vt = action(ref state, uid);
            if (vt.IsCompleted)
            {
                if (!vt.Result)
                    break;
                tasks[i] = completedTask;
            }
            else
                tasks[i] = vt.AsTask();
        }

        return Task.WhenAll(tasks.Take(i));
    }

    public void ForEachUid<TState>(ref TState state, StateAction<TState, string> action)
    {
        foreach (string uid in _uids)
        {
            action(ref state, uid);
        }
    }

    public void ForEachUid<TState>(ref TState state, StateBreakableAction<TState, string> action)
    {
        foreach (string uid in _uids)
        {
            if (!action(ref state, uid))
                break;
        }
    }

    string ITestFilter.TreePath => throw new InvalidOperationException();
    bool ITestFilter.MatchesTreePathFilter(in UnturnedTestInstance instance) => throw new InvalidOperationException();
    bool ITestFilter.MatchesTreePathFilter(in UnturnedTestInstance instance, bool needsProperties) => throw new InvalidOperationException();
}