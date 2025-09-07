using System.Collections.Concurrent;
using System.Text;

namespace uTest;

internal static class StringBuilderPool
{
    private static readonly ConcurrentBag<StringBuilder> Pool = new ConcurrentBag<StringBuilder>();

    internal static StringBuilder Rent()
    {
        if (Pool.TryTake(out StringBuilder stringBuilder))
            return stringBuilder;

        return new StringBuilder();
    }

    internal static void Return(StringBuilder stringBuilder)
    {
        stringBuilder.Clear();
        Pool.Add(stringBuilder);
    }
}
