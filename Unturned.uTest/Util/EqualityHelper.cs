using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace uTest;

internal static class EqualityHelper
{
    private static readonly MethodInfo EqualsMethod =
        typeof(EqualityHelper)
            .GetMethod(nameof(__Equals), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new NotSupportedException("Unable to find EqualityHelper.__Equals");

    private static readonly ConcurrentDictionary<Type, EqualsMethodSignature> EqualMethods
        = new ConcurrentDictionary<Type, EqualsMethodSignature>();

    private delegate bool EqualsMethodSignature(object a, object b);

    public static IEqualityComparer<T?> Default<T>()
    {
        return EqualityComparer<T?>.Default;
    }
    public static IComparer<T?> Comparer<T>()
    {
        return System.Collections.Generic.Comparer<T?>.Default;
    }

    private static bool __Equals<T>(object a, object b)
    {
        return EqualityComparer<T>.Default.Equals((T)a, (T)b);
    }

    /// <summary>
    /// Cached equality comparer using <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    public static bool ValueEquals(object? a, object? b)
    {
        if (a == b)
            return true;

        if (a == null || b == null)
            return false;

        Type type = a.GetType();
        Type bType = b.GetType();
        if (bType != type && !type.IsAssignableFrom(bType))
        {
            if (bType.IsAssignableFrom(type))
                type = bType;
            else
                return false;
        }

        EqualsMethodSignature eq = EqualMethods.GetOrAdd(
            type,
            type => (EqualsMethodSignature)EqualsMethod
                .MakeGenericMethod(type)
                .CreateDelegate(typeof(EqualsMethodSignature))
        );

        return eq(a, b);
    }

    /// <summary>
    /// Check if a string has all unique letters.
    /// </summary>
    public static bool StringUnique(string str)
    {
        bool anyCharsNotLatin = false;
        for (int i = 0; i < str.Length; ++i)
        {
            if (str[i] <= byte.MaxValue)
                continue;

            anyCharsNotLatin = true;
            break;
        }

        if (!anyCharsNotLatin)
        {
            BitArray mask = new BitArray(byte.MaxValue);
            for (int i = 0; i < str.Length; ++i)
            {
                char c = str[i];
                if (mask[c])
                    return false;

                mask[c] = true;
            }
        }
        else if (str.Length > 256)
        {
            HashSet<char> set = new HashSet<char>();
            for (int i = 0; i < str.Length; ++i)
            {
                char c = str[i];
                if (!set.Add(c))
                    return false;
            }
        }
        else
        {
            List<char> alreadyAdded = new List<char>(str.Length);
            for (int i = 0; i < str.Length; ++i)
            {
                char c = str[i];
                if (alreadyAdded.Contains(c))
                    return false;

                alreadyAdded.Add(c);
            }
        }

        return true;
    }
}
