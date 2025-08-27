using System;

namespace uTest.Util;

internal sealed class EquatableObjectList : IEquatable<EquatableObjectList?>
{
    private readonly object?[] _objects;

    public object?[] UnderlyingList => _objects;

    public EquatableObjectList(object?[] args)
    {
        _objects = args;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EquatableObjectList l && Equals(l);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hashCode = 0;
        foreach (object? obj in _objects)
        {
            if (obj != null)
                hashCode ^= obj.GetHashCode() * 397;
        }

        hashCode += _objects.Length;
        return hashCode;
    }

    /// <inheritdoc />
    public bool Equals(EquatableObjectList? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other == null)
            return false;

        if (other._objects.Length != _objects.Length)
            return false;

        for (int i = 0; i < _objects.Length; ++i)
        {
            object? obj1 = _objects[i];
            object? obj2 = other._objects[i];
            if (ReferenceEquals(obj1, obj2))
                continue;

            if (obj2 == null || obj1 == null)
                return false;

            if (!obj1.Equals(obj2))
                return false;
        }

        return true;
    }
}
