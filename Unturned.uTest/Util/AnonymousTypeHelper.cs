using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace uTest;

/// <summary>
/// Utility for getting named properties from anonymous types.
/// </summary>
internal static class AnonymousTypeHelper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> MappingCache =
        new ConcurrentDictionary<Type, PropertyInfo[]>();

    public static bool TryMapObjectToMethodParameters(object obj, MethodInfo method, MemberInfo? fromMember, out object[] args)
    {
        return TryMapObjectToMethodParameters(
            obj, method, fromMember, (method ?? throw new ArgumentNullException(nameof(method))).GetParameters(), out args
        );
    }

    public static bool TryMapObjectToMethodParameters(object obj, MethodInfo method, MemberInfo? fromMember, ParameterInfo[] parameters, out object[] args)
    {
        args = Array.Empty<object>();
        if (obj == null)
        {
            if (parameters is not [ { ParameterType.IsValueType: false } ])
                return false;

            args = new object[1];
            return true;
        }

        if (parameters.Length == 0)
            return true;

        Type type = obj.GetType();

        // tuples
        if (obj is ITuple tuple)
        {
            int tupleLength = tuple.Length;
            if (tupleLength < parameters.Length)
            {
                return false;
            }

            args = new object[parameters.Length];
            bool anyMissingMatch = false;

            // try mapping by element names first
            if (fromMember != null
                && Attribute.GetCustomAttribute(fromMember, typeof(TupleElementNamesAttribute), false) is TupleElementNamesAttribute elementNames
                && elementNames.TransformNames != null
                && elementNames.TransformNames.Count == tupleLength)
            {
                string[] names = elementNames.TransformNames as string[]
                                 ?? elementNames.TransformNames.ToArray();
                LightweightBitArray nameBitArray = new LightweightBitArray(names.Length, true);
                for (int i = 0; i < parameters.Length; ++i)
                {
                    ParameterInfo param = parameters[i];
                    int tupleElementIndex = -1;
                    for (int n = 0; n < names.Length; n++)
                    {
                        string name = names[n];
                        if (!nameBitArray[n]) continue;

                        if (!string.Equals(name, param.Name, StringComparison.OrdinalIgnoreCase))
                            continue;

                        tupleElementIndex = n;
                        if (string.Equals(name, param.Name, StringComparison.Ordinal))
                            break;
                    }

                    if (tupleElementIndex < 0)
                    {
                        anyMissingMatch = true;
                        break;
                    }

                    nameBitArray[tupleElementIndex] = false;
                    object? value = tuple[tupleElementIndex];
                    if (!TryMapValueToParameter(ref value, param))
                    {
                        return false;
                    }

                    args[i] = value;
                }

                if (!anyMissingMatch)
                {
                    return true;
                }
            }

            // then just map by order
            for (int i = 0; i < parameters.Length; ++i)
            {
                object? value = tuple[i];
                if (!TryMapValueToParameter(ref value, parameters[i]))
                {
                    return false;
                }

                args[i] = value;
            }

            return true;
        }

        // single value
        if (parameters.Length == 1 && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            ParameterInfo param = parameters[0];
            object value = obj;
            if (TryMapValueToParameter(ref value, param))
            {
                args = [ value ];
                return true;
            }
        }

        // anonymous types
        PropertyInfo[] properties = MappingCache.GetOrAdd(type, type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        LightweightBitArray bitArray = new LightweightBitArray(properties.Length, true);
        object[] arguments = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; ++i)
        {
            ParameterInfo param = parameters[i];
            int propIndex = -1;
            for (int p = 0; p < properties.Length; p++)
            {
                PropertyInfo property = properties[p];
                if (!bitArray[p]) continue;

                if (!string.Equals(property.Name, param.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                propIndex = p;
                if (string.Equals(property.Name, param.Name, StringComparison.Ordinal))
                    break;
            }

            if (propIndex < 0)
            {
                return false;
            }

            PropertyInfo leadingProperty = properties[propIndex];
            bitArray[propIndex] = false;
            object? value = leadingProperty.GetValue(obj);
            if (!TryMapValueToParameter(ref value, param))
            {
                return false;
            }

            arguments[i] = value;
        }

        args = arguments;
        return true;
    }

    private static bool TryMapValueToParameter(ref object value, ParameterInfo param)
    {
        if (value == null)
        {
            return !param.ParameterType.IsValueType;
        }

        if (param.ParameterType.IsInstanceOfType(value))
            return true;

        try
        {
            value = Convert.ChangeType(value, param.ParameterType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return false;
        }

        return true;
    }
}