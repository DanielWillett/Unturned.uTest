using System;
using System.Text;
using uTest.Discovery;

namespace uTest;

internal static class FilterHelper
{
    private struct RemoveUnfilteredTestsUidListState
    {
        public bool Exists;
        public UnturnedTest Test;
    }

    public static void RemoveUnfilteredTests(ITestFilter filter, List<UnturnedTest> tests, int startIndex, int length = -1)
    {
        if (length < 0)
            length = tests.Count - startIndex;
        length += startIndex;
        RemoveUnfilteredTestsUidListState state;
        switch (filter.Type)
        {
            case TestFilterType.UidList:
                for (int i = length - 1; i >= startIndex; --i)
                {
                    UnturnedTest test = tests[i];
                    state.Test = test;
                    state.Exists = false;
                    if (test.Expandable)
                    {
                        filter.ForEachUid(ref state, static (ref state, uid) =>
                        {
                            if (!UnturnedTestUid.IsSameBaseMethod(state.Test.Uid, uid, allowTypeDefinitionMatching: state.Test.Owner!.TypeParameters is { Length: > 0 }))
                                return true;

                            state.Exists = true;
                            return false;
                        });
                    }
                    else
                    {
                        filter.ForEachUid(ref state, static (ref state, uid) =>
                        {
                            if (!string.Equals(uid, state.Test.Uid, StringComparison.Ordinal))
                                return true;

                            state.Exists = true;
                            return false;
                        });
                    }

                    if (!state.Exists)
                    {
                        tests.RemoveAt(i);
                    }
                }

                break;

            case TestFilterType.TreePath:
                string treeFilter = filter.TreePath;
                bool needsProperties = treeFilter.IndexOf('[') >= 0;
                for (int i = length - 1; i >= startIndex; --i)
                {
                    UnturnedTest test = tests[i];
                    if (test.Expandable)
                    {
                        // a node type tree filter would look something like this:
                        //  /Assembly/Namespace/Name/Method/...
                        //  |         type         | |mtd | |type args ...
                        // get index of fourth slash
                        int slashes = string.IsNullOrEmpty(test.Owner!.Type.Namespace) ? 4 : 5;
                        if (TreeFiltersWithinNSlashes(treeFilter, slashes, out ReadOnlySpan<char> filterPortion))
                            continue;

                        string tree = TreeNodeFilterHelper.GetMethodFilter(test.Method, useWildcards: false, writeFinalSlash: true);
                        if (tree.AsSpan().StartsWith(filterPortion, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    else
                    {
                        UnturnedTestInstance instance = new UnturnedTestInstance(test);
                        if (filter.MatchesTreePathFilter(in instance, needsProperties))
                            continue;
                    }

                    tests.RemoveAt(i);
                }
                break;
        }
    }

    private struct PotentiallyMatchesFilterUidListState
    {
        public bool Exists;
        public Type TestType;
    }

    public static bool PotentiallyMatchesFilter(Type testType, ITestFilter filter)
    {
        switch (filter.Type)
        {
            case TestFilterType.UidList:

                PotentiallyMatchesFilterUidListState state;
                state.TestType = testType;
                state.Exists = false;

                filter.ForEachUid(ref state,
                    testType.IsGenericTypeDefinition
                    ? static (ref state, uid) =>
                    {
                        if (!UnturnedTestUid.IsSameTypeDefinitionAs(uid, state.TestType))
                            return true;

                        state.Exists = true;
                        return false;
                    }
                    : static (ref state, uid) =>
                    {
                        if (!UnturnedTestUid.IsSameTypeAs(uid, state.TestType))
                            return true;

                        state.Exists = true;
                        return false;
                    });

                return state.Exists;

            case TestFilterType.TreePath:

                // a node type tree filter would look something like this:
                //  /Assembly/Namespace/Name/Method/...
                //  |         type         | | method and type args ...
                // get index of third slash
                int slashes = string.IsNullOrEmpty(testType.Namespace) ? 3 : 4;
                if (TreeFiltersWithinNSlashes(filter.TreePath, slashes, out ReadOnlySpan<char> filterPortion))
                {
                    return true;
                }

                string tree = TreeNodeFilterHelper.GetTypeFilter(testType, useWildcards: false, writeFinalSlash: true);
                return tree.AsSpan().StartsWith(filterPortion, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static int FirstSelectionIndex(string filter)
    {
        return filter.AsSpan().IndexOfAny([ '[', '&', '|', '%', '(', '=', '*', ']', ')' ]);
    }

    private static bool TreeFiltersWithinNSlashes(string filter, int n, out ReadOnlySpan<char> filterPortion)
    {
        int firstSpecialCheck = FirstSelectionIndex(filter);

        int lastSlash = 0;
        // first char is always '/'.
        for (int i = 1; i < n; ++i)
        {
            int nextSlash = filter.IndexOf('/', lastSlash + 1);
            if (nextSlash == -1)
            {
                lastSlash = filter.Length;
                break;
            }

            lastSlash = nextSlash;
        }

        int index = firstSpecialCheck < 0 ? lastSlash : Math.Min(firstSpecialCheck, lastSlash);
        if (index < filter.Length)
        {
            if (filter[index] == '/')
                ++index;
            filterPortion = filter.AsSpan(0, index);
        }
        else if (filter[^1] == '/')
        {
            filterPortion = (filter + "/").AsSpan();
        }
        else
        {
            filterPortion = filter.AsSpan();
        }
        return firstSpecialCheck >= 0 && firstSpecialCheck < lastSlash;
    }

    public static bool TreeCouldContainThisTypeConfiguration(UnturnedTest test, Type[] typeArgs, Type[]? methodArgs, object?[]? parameters, string filter)
    {
        // a node type tree filter would look something like this:
        //  /Namespace/Name/Method/<TypeArgs>/<MethodArgs>/(Parameters)

        string tree;
        ReadOnlySpan<char> filterPortion;

        int slashes = string.IsNullOrEmpty(test.Owner!.Type.Namespace) ? 4 : 5;
        if (typeArgs.Length == 0 && methodArgs is not { Length: > 0 })
        {
            if (TreeFiltersWithinNSlashes(filter, slashes, out filterPortion))
                return true;

            tree = TreeNodeFilterHelper.GetMethodFilter(test.Method, useWildcards: false);
        }
        else
        {
            StringBuilder sb = StringBuilderPool.Rent();

            TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);

            TreeNodeFilterHelper.WriteMethodPrefix(test.Method, ref writer, false, false);
            if (typeArgs.Length > 0)
            {
                ++slashes;
                writer.WriteSeparator();
                foreach (Type t in typeArgs)
                {
                    writer.WriteTypeName(ManagedIdentifier.GetManagedType(t));
                }
            }

            if (methodArgs is { Length: > 0 })
            {
                ++slashes;
                writer.WriteSeparator();
                foreach (Type t in typeArgs)
                {
                    writer.WriteTypeName(ManagedIdentifier.GetManagedType(t));
                }
            }

            if (parameters is { Length: > 0 })
            {
                ++slashes;
                writer.WriteSeparator();
                for (int i = 0; i < parameters.Length; ++i)
                {
                    writer.WriteParameterValue(ref parameters[i], static (ref object? state, StringBuilder sb) =>
                    {
                        UnturnedTestUid.WriteParameterForTreeNode(state, sb);
                    });
                }
            }


            if (TreeFiltersWithinNSlashes(filter, slashes, out filterPortion))
            {
                StringBuilderPool.Return(sb);
                return true;
            }

            writer.WriteSeparator();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            if (sb.Length <= 255)
            {
                writer.Flush();
                Span<char> text = stackalloc char[sb.Length];
                sb.CopyTo(0, text, text.Length);
                StringBuilderPool.Return(sb);
                return ((ReadOnlySpan<char>)text).StartsWith(filterPortion, StringComparison.OrdinalIgnoreCase);
            }
#endif

            tree = writer.FlushToString();
            StringBuilderPool.Return(sb);
        }

        return tree.AsSpan().StartsWith(filterPortion, StringComparison.OrdinalIgnoreCase);
    }
}
