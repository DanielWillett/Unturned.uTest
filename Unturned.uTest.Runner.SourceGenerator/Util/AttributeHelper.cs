using Microsoft.CodeAnalysis;

namespace uTest.Util;

internal static class AttributeHelper
{
    /// <remarks>Does not support arrays</remarks>
    public static bool TryReadTypedConstant<TValue>(in TypedConstant constant, out TValue? value)
    {
        switch (constant.Kind)
        {
            case TypedConstantKind.Enum:
                if (!typeof(TValue).IsEnum)
                    break;

                value = (TValue)constant.Value!;
                return true;

            case TypedConstantKind.Type:
                if (!typeof(TValue).IsAssignableFrom(typeof(ITypeSymbol)))
                    break;

                value = (TValue?)(constant.Value as ITypeSymbol);
                return true;

            case TypedConstantKind.Primitive:
                if (constant.Value is not TValue v)
                    break;

                value = v;
                return true;
        }

        value = default;
        return false;
    }
}