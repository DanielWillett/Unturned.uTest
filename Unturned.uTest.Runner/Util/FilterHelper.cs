using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using System.Reflection;
using System.Text;

namespace uTest.Runner.Util;

#pragma warning disable TPEXP

internal static class FilterHelper
{
    private static readonly PropertyBag EmptyBag = new PropertyBag();
    private static ConstructorInfo? _treeCtor;

    public static TreeNodeFilter CreateTreeFilter(string filter)
    {
        _treeCtor ??= typeof(TreeNodeFilter).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [ typeof(string) ], null);

        if (_treeCtor == null)
            throw new MissingMethodException(nameof(TreeNodeFilter), ".ctor(string)");

        return (TreeNodeFilter)_treeCtor.Invoke([ filter ]);
    }

    public static void RemoveUnfilteredTests(ITestExecutionFilter filter, List<UnturnedTest> tests, int startIndex, int length = -1)
    {
        if (length < 0)
            length = tests.Count - startIndex;
        length += startIndex;
        switch (filter)
        {
            case TestNodeUidListFilter listFilter:
                TestNodeUid[] uids = listFilter.TestNodeUids;
                for (int i = length - 1; i >= startIndex; --i)
                {
                    UnturnedTest test = tests[i];
                    int index = -1;
                    if (test.Expandable)
                    {
                        for (int x = 0; x < uids.Length; ++x)
                        {
                            if (!UnturnedTestUid.IsSameBaseMethod(test.Uid, uids[x].Value, allowTypeDefinitionMatching: test.Owner!.TypeParameters is { Length: > 0 }))
                                continue;

                            index = x;
                            break;
                        }
                    }
                    else
                    {
                        for (int x = 0; x < uids.Length; ++x)
                        {
                            if (!string.Equals(uids[x].Value, test.Uid, StringComparison.Ordinal))
                                continue;

                            index = x;
                            break;
                        }
                    }

                    if (index >= 0)
                        continue;

                    tests.RemoveAt(i);
                }

                break;

            case TreeNodeFilter treeFilter:
                bool needsProperties = treeFilter.Filter.IndexOf('[') >= 0;
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
                        PropertyBag bag = EmptyBag;
                        if (needsProperties)
                        {
                            bag = new PropertyBag();
                            new UnturnedTestInstance(test).AddProperties(bag);
                        }

                        if (treeFilter.MatchesFilter(test.TreePath, bag!))
                            continue;
                    }

                    tests.RemoveAt(i);
                }
                break;
        }
    }

    public static bool PotentiallyMatchesFilter(Type testType, ITestExecutionFilter filter)
    {
        switch (filter)
        {
            case TestNodeUidListFilter listFilter:
                
                return Array.Exists(
                    listFilter.TestNodeUids,
                    testType.IsGenericTypeDefinition
                        ? uid => UnturnedTestUid.IsSameTypeDefinitionAs(uid.Value, testType)
                        : uid => UnturnedTestUid.IsSameTypeAs(uid.Value, testType)
                );

            case TreeNodeFilter treeFilter:

                // a node type tree filter would look something like this:
                //  /Assembly/Namespace/Name/Method/...
                //  |         type         | | method and type args ...
                // get index of third slash
                int slashes = string.IsNullOrEmpty(testType.Namespace) ? 3 : 4;
                if (TreeFiltersWithinNSlashes(treeFilter, slashes, out ReadOnlySpan<char> filterPortion))
                {
                    return true;
                }

                string tree = TreeNodeFilterHelper.GetTypeFilter(testType, useWildcards: false, writeFinalSlash: true);
                return tree.AsSpan().StartsWith(filterPortion, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static int FirstSelectionIndex(TreeNodeFilter filter)
    {
        return filter.Filter.AsSpan().IndexOfAny(['[', '&', '|', '%', '(', '=', '*', ']', ')']);
    }

    private static bool TreeFiltersWithinNSlashes(TreeNodeFilter filter, int n, out ReadOnlySpan<char> filterPortion)
    {
        int firstSpecialCheck = FirstSelectionIndex(filter);

        int lastSlash = 0;
        // first char is always '/'.
        for (int i = 1; i < n; ++i)
        {
            int nextSlash = filter.Filter.IndexOf('/', lastSlash + 1);
            if (nextSlash == -1)
            {
                lastSlash = filter.Filter.Length;
                break;
            }

            lastSlash = nextSlash;
        }

        int index = firstSpecialCheck < 0 ? lastSlash : Math.Min(firstSpecialCheck, lastSlash);
        if (index < filter.Filter.Length)
        {
            if (filter.Filter[index] == '/')
                ++index;
            filterPortion = filter.Filter.AsSpan(0, index);
        }
        else if (filter.Filter[^1] == '/')
        {
            filterPortion = (filter.Filter + "/").AsSpan();
        }
        else
        {
            filterPortion = filter.Filter.AsSpan();
        }
        return firstSpecialCheck >= 0 && firstSpecialCheck < lastSlash;
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

    public static bool TreeCouldContainThisTypeConfiguration(UnturnedTest test, Type[] typeArgs, Type[]? methodArgs, object?[]? parameters, TreeNodeFilter filter)
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
