using System.Collections.Concurrent;
using System.Collections.Generic;

namespace uTest.Util;

internal static class StackPool<T>
{
    private static readonly ConcurrentBag<Stack<T>> Pool = new ConcurrentBag<Stack<T>>();

    internal static Stack<T> Rent()
    {
        if (Pool.TryTake(out Stack<T> stack))
            return stack;

        return new Stack<T>();
    }

    internal static void Return(Stack<T> stack)
    {
        Pool.Add(stack);
    }
}
