using JetBrains.Annotations;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace uTest.Runner.Util;

/// <summary>
/// Utilities used by code emitted from source-generators.
/// </summary>
/// <remarks>Everything in here is an internal API and is subject to change, and therefore should not be used by user code.</remarks>
[EditorBrowsable(EditorBrowsableState.Never), UsedImplicitly]
public static class SourceGenerationServices
{
    [UsedImplicitly]
    public static MethodInfo GetMethodByExpression<TObject, TDelegate>(Expression<Func<TObject, TDelegate>> expr)
    {
        return expr.Body is UnaryExpression { Operand: MethodCallExpression { Object: ConstantExpression { Value: MethodInfo method } } }
            ? method
            : throw new MemberAccessException("Failed to parse method expression.");
    }

    [UsedImplicitly]
    public static MethodInfo GetMethodByDelegate(Delegate d)
    {
        return d?.Method ?? throw new MemberAccessException();
    }
}
