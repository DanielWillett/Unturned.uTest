using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using System.Reflection;
using uTest.Discovery;

namespace uTest.Runner.Util;

#pragma warning disable TPEXP

internal static class MTPFilterHelper
{
    internal static readonly PropertyBag EmptyBag = new PropertyBag();
    private static ConstructorInfo? _treeCtor;

    public static TreeNodeFilter CreateTreeFilter(string filter)
    {
        _treeCtor ??= typeof(TreeNodeFilter).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null);

        if (_treeCtor == null)
            throw new MissingMethodException(nameof(TreeNodeFilter), ".ctor(string)");

        return (TreeNodeFilter)_treeCtor.Invoke([filter]);
    }

    public static bool MatchesFilter(TreeNodeFilter filter, in UnturnedTestInstance instance)
    {
        bool needsPropertyBag = filter.Filter.IndexOf('[') >= 0;
        PropertyBag bag = EmptyBag;
        if (needsPropertyBag)
        {
            bag = new PropertyBag();
            instance.AddProperties(bag);
        }

        return filter.MatchesFilter(instance.TreePath, bag);
    }

    public static ITestFilter? CreateFilter(ITestExecutionFilter mtpFilter)
    {
        switch (mtpFilter)
        {
            case TestNodeUidListFilter list:
                TestNodeUid[] listTestNodeUids = list.TestNodeUids;

                string[] args = new string[listTestNodeUids.Length];
                for (int i = 0; i < args.Length; ++i)
                    args[i] = listTestNodeUids[i].Value;

                return new UidListFilter(args);

            case TreeNodeFilter tree:
                return new TreeNodeFilterWrapper(tree);
        }

        return null;
    }
}

internal class TreeNodeFilterWrapper(TreeNodeFilter tree) : ITestFilter
{
    public TestFilterType Type => TestFilterType.TreePath;

    int ITestFilter.UidCount => throw new InvalidOperationException();

    Task ITestFilter.ForEachUid<TState>(ref TState state, StateAsyncAction<TState, string> action) => throw new InvalidOperationException();

    Task ITestFilter.ForEachUid<TState>(ref TState state, StateBreakableAsyncAction<TState, string> action) => throw new InvalidOperationException();

    void ITestFilter.ForEachUid<TState>(ref TState state, StateAction<TState, string> action) => throw new InvalidOperationException();

    void ITestFilter.ForEachUid<TState>(ref TState state, StateBreakableAction<TState, string> action) => throw new InvalidOperationException();

    public string TreePath => tree.Filter;

    public bool MatchesTreePathFilter(in UnturnedTestInstance instance)
    {
        return MTPFilterHelper.MatchesFilter(tree, in instance);
    }

    public bool MatchesTreePathFilter(in UnturnedTestInstance instance, bool needsProperties)
    {
        PropertyBag bag = MTPFilterHelper.EmptyBag;
        if (needsProperties)
        {
            bag = new PropertyBag();
            instance.AddProperties(bag);
        }

        return tree.MatchesFilter(instance.TreePath, bag);
    }
}