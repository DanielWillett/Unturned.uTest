using System;
using System.Collections.Generic;
using System.Linq;

namespace uTest.Util;

/// <remarks>
/// Mostly stolen from <see href="https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md#auto-interface-implementation"/>.
/// </remarks>
public class EquatableList<T> : List<T>, IEquatable<EquatableList<T>> where T : IEquatable<T>
{
    public EquatableList() { }
    public EquatableList(int capacity) : base(capacity) { }
    public EquatableList(IEnumerable<T> collection) : base(collection) { }

    public bool Equals(EquatableList<T>? other)
    {
        if (other is null || Count != other.Count)
        {
            return false;
        }

        for (int i = 0; i < Count; i++)
        {
            T t0 = this[i];
            T t1 = other[i];
            if (!typeof(T).IsValueType)
            {
                if (t0 == null)
                {
                    if (t1 != null)
                        return false;
                    continue;
                }
                if (t1 == null)
                    return false;
            }
            if (!t0.Equals(t1))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EquatableList<T>);
    }

    public override int GetHashCode()
    {
        return this.Select(item => item?.GetHashCode() ?? 0).Aggregate((x, y) => x ^ y);
    }

    public static bool operator ==(EquatableList<T>? list1, EquatableList<T>? list2)
    {
        return ReferenceEquals(list1, list2)
               || list1 is not null && list2 is not null && list1.Equals(list2);
    }

    public static bool operator !=(EquatableList<T>? list1, EquatableList<T>? list2)
    {
        return !(list1 == list2);
    }
}