using System;

namespace uTest;

internal static class TypeKeywordHelper
{
    internal static string? GetTypeKeyword(Type type)
    {
        if (type.IsPrimitive)
        {
            if (type == typeof(bool))
                return "bool";
            if (type == typeof(byte))
                return "byte";
            if (type == typeof(char))
                return "char";
            if (type == typeof(double))
                return "double";
            if (type == typeof(short))
                return "short";
            if (type == typeof(int))
                return "int";
            if (type == typeof(long))
                return "long";
            if (type == typeof(IntPtr))
                return "nint";
            if (type == typeof(sbyte))
                return "sbyte";
            if (type == typeof(float))
                return "float";
            if (type == typeof(ushort))
                return "ushort";
            if (type == typeof(uint))
                return "uint";
            if (type == typeof(ulong))
                return "ulong";
            if (type == typeof(UIntPtr))
                return "nuint";
        }
        else if (type.IsClass)
        {
            if (type == typeof(object))
                return "object";
            if (type == typeof(string))
                return "string";
        }
        else if (type == typeof(decimal))
            return "decimal";

        return null;
    }
}