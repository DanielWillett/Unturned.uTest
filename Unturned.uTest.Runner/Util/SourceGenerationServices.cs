using JetBrains.Annotations;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

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
        MethodInfo m = expr.Body is UnaryExpression { Operand: MethodCallExpression { Object: ConstantExpression { Value: MethodInfo method } } }
            ? method
            : throw new MemberAccessException("Failed to parse method expression.");

        if (m.DeclaringType is { IsConstructedGenericType: true })
            m = (MethodInfo)m.Module.ResolveMethod(m.MetadataToken);
        else if (m.IsGenericMethod)
            m = m.GetGenericMethodDefinition();

        return m;
    }

    private static unsafe StringBuilder AppendSpan(StringBuilder builder, ReadOnlySpan<char> span)
    {
        fixed (char* ptr = span)
        {
            builder.Append(ptr, span.Length);
        }

        return builder;
    }

    public static MethodInfo GetMethodInfoByManagedMethod(Type type, string managedMethod)
    {
        return GetMethodInfoByManagedMethod(
            type,
            type.GetMethods(BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Instance
                            | BindingFlags.DeclaredOnly),
            managedMethod
        );
    }

    public static MethodInfo GetMethodInfoByManagedMethod(Type type, MethodInfo[] methods, string managedMethod)
    {
        ManagedMethodTokenizer tokenizer = new ManagedMethodTokenizer(managedMethod.AsSpan(), false);

        StringBuilder? valueBuilder = null;
        string? methodName = null;
        int methodArity = 0;
        bool doBreak = false;
        while (tokenizer.MoveNext() && !doBreak)
        {
            switch (tokenizer.TokenType)
            {
                case ManagedMethodTokenType.MethodImplementationTypeSegment when methodName == null:
                    if (valueBuilder == null)
                    {
                        valueBuilder = AppendSpan(new StringBuilder(), tokenizer.Value);
                    }
                    else
                    {
                        AppendSpan(valueBuilder.Append('.'), tokenizer.Value);
                    }

                    break;

                case ManagedMethodTokenType.Arity:
                    methodArity = tokenizer.Arity;
                    break;
                
                case ManagedMethodTokenType.MethodName:
                    if (valueBuilder != null)
                    {
                        methodName = AppendSpan(valueBuilder.Append('.'), tokenizer.Value).ToString();
                    }
                    else
                    {
                        methodName = tokenizer.Value.ToString();
                    }

                    break;

                case ManagedMethodTokenType.OpenParameters:
                    doBreak = true;
                    break;
            }
        }

        valueBuilder?.Clear();

        if (methodName != null)
        {
            MethodInfo? bestCandidate = null;

            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                if (methodArity != method.GetGenericArguments().Length)
                    continue;

                ManagedMethodTokenizer t2 = tokenizer;
                ParameterInfo[] parameters = method.GetParameters();
                bool isMatch = true;
                int parameterCount = 0;
                if (t2.TokenType is not ManagedMethodTokenType.CloseParameters and not ManagedMethodTokenType.Uninitialized)
                {
                    while (true)
                    {
                        if (parameterCount >= parameters.Length
                            || !t2.IsSameTypeAs(parameters[parameterCount].ParameterType, valueBuilder))
                        {
                            isMatch = false;
                            break;
                        }

                        ++parameterCount;

                        if (t2.TokenType is ManagedMethodTokenType.CloseParameters or ManagedMethodTokenType.Uninitialized)
                            break;
                    }

                    if (parameterCount < parameters.Length)
                        isMatch = false;
                }
                else if (parameters.Length != 0)
                    continue;

                if (!isMatch)
                    continue;

                if (bestCandidate == null)
                    bestCandidate = method;
                else
                {
                    bestCandidate = null;
                    break;
                }
            }

            if (bestCandidate != null)
                return bestCandidate;
        }

        throw new MissingMethodException($"Unable to identify method {managedMethod} in type {type.FullName}.");
    }

    [UsedImplicitly]
    public static MethodInfo GetMethodByDelegate(Delegate d)
    {
        return d?.Method ?? throw new MemberAccessException();
    }
}