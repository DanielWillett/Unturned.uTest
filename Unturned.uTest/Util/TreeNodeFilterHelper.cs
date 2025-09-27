using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;

namespace uTest;

/// <summary>
/// Utilities for working with the 'TreeNodeFilter' class from MTP.
/// </summary>
public static partial class TreeNodeFilterHelper
{
    private static int GetArity(Type type)
    {
        int arity = 0;
        try
        {
            arity = type.GetGenericArguments().Length;
        }
        catch { /* ignored */ }
        if (type.IsNested)
        {
            try
            {
                arity -= type.DeclaringType!.GetGenericArguments().Length;
            }
            catch { /* ignored */ }
        }

        return arity;
    }

    private static int GetArity(MethodBase method)
    {
        int arity = 0;
        try
        {
            arity = method.GetGenericArguments().Length;
        }
        catch { /* ignored */ }

        return arity;
    }

    // (escaped obviously, values use the same format as UIDs except never use indices)
    // /Assembly/Namespace/Type/Test1
    // /Assembly/Namespace/Type<,>/Test2/<int,string>
    // /Assembly/Namespace/Type<,>/Test2/<string,long>
    // /Assembly/Namespace/Type<,>/Test2<>/<string,long>/<DateTime>
    // /Assembly/Namespace/Type<,>/Test2<>(string,int)/<string,long>/<DateTime>/("Value",123)
    // (if declaring type is null)
    // /Assembly/Test2<>(string,int)/<DateTime>/("Value",123)
    // (if namespace is empty)
    // /Assembly/Type/Test1

    /// <summary>
    /// Gets a tree path filter that matches all tests in this type.
    /// </summary>
    /// <remarks>Generic type parameters are taken into account.</remarks>
    public static string GetTypeFilter(Type type, bool useWildcards = true, bool writeFinalSlash = false)
    {
        StringBuilder sb = StringBuilderPool.Rent();

        TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);

        WriteTypePrefix(type, ref writer);

        if (useWildcards)
        {
            if (type.IsConstructedGenericType)
            {
                Type[] args = type.GetGenericArguments();
                writer.WriteWildcard(false);
                foreach (Type t in args)
                    writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t));
            }

            writer.WriteWildcard(true);
        }

        if (writeFinalSlash && sb[^1] != '/')
            writer.WriteSeparator();

        string str = writer.FlushToString();

        StringBuilderPool.Return(sb);
        return str;
    }

    /// <summary>
    /// Gets a tree path filter that matches all tests using this method.
    /// </summary>
    /// <remarks>Generic type parameters are taken into account.</remarks>
    public static string GetMethodFilter(MethodInfo method, bool useWildcards = true, bool writeFinalSlash = false)
    {
        StringBuilder sb = StringBuilderPool.Rent();

        TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);

        WriteMethodPrefix(method.GetParameters(), method, ref writer, useWildcards, useWildcards);

        if (writeFinalSlash && sb[^1] != '/')
            writer.WriteSeparator();

        string str = writer.FlushToString();

        StringBuilderPool.Return(sb);
        return str;
    }

    public delegate void WriteParameter<TState>(ParameterInfo parameter, ref TState state, StringBuilder builder);

    /// <summary>
    /// Gets a tree path filter that matches all tests using this method.
    /// </summary>
    /// <remarks>Generic type parameters are taken into account.</remarks>
    public static string GetMethodFilter<TState>(MethodInfo method, ref TState state, WriteParameter<TState> parameterWriter)
    {
        StringBuilder sb = StringBuilderPool.Rent();

        TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);

        ParameterInfo[] parameters = method.GetParameters();
        WriteMethodPrefix(parameters, method, ref writer, true, false);

        foreach (ParameterInfo parameter in parameters)
        {
            WriteParameterState<TState> writeState;
            writeState.Parameter = parameter;
            writeState.Writer = parameterWriter;
            writer.WriteParameterValue(
                ref state,
                ref writeState,
                static (ref state1, ref state2, builder) =>
                {
                    state2.Writer.Invoke(state2.Parameter, ref state1, builder);
                }
            );
        }

        string str = writer.FlushToString();

        StringBuilderPool.Return(sb);
        return str;
    }

    private struct WriteParameterState<TState>
    {
        public WriteParameter<TState> Writer;
        public ParameterInfo Parameter;
    }

    internal static void WriteTypePrefix(Type type, ref TreeNodeFilterWriter writer)
    {
        writer.WriteAssemblyName(type.Assembly.GetName().Name);
        writer.WriteNamespace(type.Namespace);
        bool isNested = false;
        if (type.IsNested)
        {
            Stack<Type> nestedTypeStack = StackPool<Type>.Rent();
            for (Type? t = type.DeclaringType; t != null; t = t.DeclaringType)
            {
                nestedTypeStack.Push(t);
            }

            while (nestedTypeStack.Count > 0)
            {
                Type t = nestedTypeStack.Pop();
                writer.WriteTypeName(t.Name, GetArity(t), isNested: isNested);
                isNested = true;
            }

            StackPool<Type>.Return(nestedTypeStack);
        }

        writer.WriteTypeName(type.Name, GetArity(type), isNested: isNested);
    }

    internal static void WriteMethodPrefix(MethodInfo method, ref TreeNodeFilterWriter writer, bool genericsWildcard, bool parameterWildcard)
    {
        WriteMethodPrefix(method.GetParameters(), method, ref writer, genericsWildcard, parameterWildcard);
    }
    private static void WriteMethodPrefix(ParameterInfo[] parameters, MethodInfo method, ref TreeNodeFilterWriter writer, bool genericsWildcard, bool parameterWildcard)
    {
        Type? type = method.DeclaringType;

        if (type != null)
            WriteTypePrefix(type, ref writer);

        writer.WriteMethodName(method.Name, GetArity(method));

        // reduce method declared in constructed generic type to it's base definition
        Type? genTypeDef = parameters.Length > 0 && type is { IsConstructedGenericType: true } ? type.GetGenericTypeDefinition() : null;
        if (genTypeDef != null && MethodBase.GetMethodFromHandle(method.MethodHandle, genTypeDef.TypeHandle) is MethodInfo mtd)
        {
            if (mtd is { IsGenericMethod: true, IsGenericMethodDefinition: false })
                mtd = mtd.GetGenericMethodDefinition();
            parameters = mtd.GetParameters();
        }
        else if (method is { IsGenericMethod: true, IsGenericMethodDefinition: false })
            parameters = method.GetGenericMethodDefinition().GetParameters();

        foreach (ParameterInfo parameter in parameters)
        {
            writer.WriteParameterType(ManagedIdentifier.GetManagedType(parameter.ParameterType));
        }

        if (!genericsWildcard && !parameterWildcard)
            return;

        Type[] genericTypeArguments;
        if (type is not { IsConstructedGenericType: true })
        {
            if (genericsWildcard)
            {
                // declaring type parameters
                if (type is { IsGenericTypeDefinition: true })
                {
                    writer.WriteWildcard(false);
                }

                // method type parameters
                if (method is { IsGenericMethod: true })
                {
                    if (method.IsGenericMethodDefinition)
                        writer.WriteWildcard(false);
                    else
                    {
                        genericTypeArguments = method.GetGenericArguments();
                        foreach (Type t in genericTypeArguments)
                            writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t));
                    }
                }
            }

            // parameters
            if (parameterWildcard && parameters.Length > 0)
            {
                writer.WriteWildcard(!genericsWildcard);
            }

            return;
        }
        
        // declaring type parameters
        genericTypeArguments = type.GetGenericArguments();
        foreach (Type t in genericTypeArguments)
            writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t));

        // method type parameters
        if (method is { IsGenericMethod: true })
        {
            if (method.IsGenericMethodDefinition)
            {
                if (genericsWildcard)
                    writer.WriteWildcard(false);
            }
            else
            {
                writer.WriteSeparator();
                genericTypeArguments = method.GetGenericArguments();
                foreach (Type t in genericTypeArguments)
                    writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t));
            }
        }

        // parameters
        if (parameterWildcard && parameters.Length > 0)
        {
            writer.WriteWildcard(false);
        }
    }

    /// <summary>
    /// Decides whether or not some text needs escaping.
    /// </summary>
    public static bool NeedsEscape(ReadOnlySpan<char> text)
    {
        return text.IndexOfAny([ '\\', '*', '%', '[', ']', '/', '=', '(', ')', '|', '&', '!', '+' ]) >= 0;
    }

    /// <summary>
    /// Escapes a raw string to be written to the writer.
    /// </summary>
    /// <remarks>Needed for <see cref="TreeNodeFilterWriter.WriteParameterValue{TState}"/>.</remarks>
    public static string Escape(string str)
    {
        if (!NeedsEscape(str.AsSpan()))
            return str;

        string escaped = WebUtility.UrlEncode(str)!;

        if (escaped.AsSpan().IndexOfAny([ '*', '!', '(', ')' ]) < 0)
            return escaped;

        // it doesn't escape these for some reason
        return escaped
            .Replace("*", "%2A")
            .Replace("!", "%21")
            .Replace("(", "%28")
            .Replace(")", "%29");
    }

    /// <summary>
    /// Escapes a raw string to be written to the writer.
    /// </summary>
    /// <remarks>Needed for <see cref="TreeNodeFilterWriter.WriteParameterValue{TState}"/>.</remarks>
    public static ReadOnlySpan<char> Escape(ReadOnlySpan<char> str)
    {
        if (!NeedsEscape(str))
            return str;

        return WebUtility.UrlEncode(str.ToString())!.Replace("*", "%2A");
    }
}

public struct TreeNodeFilterWriter
{
    private readonly StringBuilder _stringBuilder;
    private bool _hasSlash;
    private int _parameterTypeIndex;
    private int _parameterIndex;
    private int _genericParameterIndex;

    public TreeNodeFilterWriter(StringBuilder stringBuilder)
    {
        _stringBuilder = stringBuilder;
        _parameterTypeIndex = -1;
        _genericParameterIndex = 0;
        _parameterIndex = 0;
    }

    public void Flush()
    {
        if (_parameterTypeIndex > 0)
        {
            _stringBuilder.Append("%29" /* ) */);
            _parameterTypeIndex = -1;
            _hasSlash = false;
        }
        if (_genericParameterIndex > 0)
        {
            _stringBuilder.Append('>');
            _genericParameterIndex = 0;
            _hasSlash = false;
        }
        if (_parameterIndex > 0)
        {
            _stringBuilder.Append("%29" /* ) */);
            _parameterIndex = 0;
            _hasSlash = false;
        }
    }

    public void WriteSeparator()
    {
        Flush();
        _stringBuilder.Append('/');
        _hasSlash = true;
    }

    public void WriteWildcard(bool multipleSegments)
    {
        if (!_hasSlash) WriteSeparator();
        _stringBuilder.Append('*', 1 + (multipleSegments ? 1 : 0));
        _hasSlash = false;
    }

    public void WriteAssemblyName(string assemblyName)
    {
        if (!_hasSlash) WriteSeparator();
        string encoded = TreeNodeFilterHelper.Escape(assemblyName);
        _stringBuilder.Append(encoded);
        _hasSlash = false;
    }

    public void WriteNamespace(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
            return;

        if (!_hasSlash) WriteSeparator();
        string encoded = TreeNodeFilterHelper.Escape(ns!);
        _stringBuilder.Append(encoded);
        _hasSlash = false;
    }

    public void WriteTypeName(string typeName, int arity = 0, bool isNested = false)
    {
        if (isNested)
        {
            if (_hasSlash)
            {
                _stringBuilder.Remove(_stringBuilder.Length - 1, 1);
                _hasSlash = false;
            }
            else
            {
                Flush();
            }

            _stringBuilder.Append("%2B" /* + */);
        }
        else if (!_hasSlash) WriteSeparator();

        int elementTypeIndex = typeName.AsSpan().IndexOfAny([ '[', '*', '&' ]);
        ReadOnlySpan<char> typeNameSpan = typeName;
        if (elementTypeIndex >= 0)
        {
            typeNameSpan = typeNameSpan.Slice(0, elementTypeIndex);
        }


        ReadOnlySpan<char> withoutArity = ManagedIdentifier.TryRemoveArity(typeNameSpan, arity);
        if (withoutArity.Length == typeName.Length)
        {
            _stringBuilder.Append(TreeNodeFilterHelper.Escape(typeName));
        }
        else if (withoutArity.Length == typeNameSpan.Length)
        {
            ManagedIdentifier.AppendSpan(_stringBuilder, TreeNodeFilterHelper.Escape(typeNameSpan));
        }
        else if (TreeNodeFilterHelper.NeedsEscape(withoutArity))
        {
            _stringBuilder.Append(WebUtility.UrlEncode(withoutArity.ToString()));
        }
        else
        {
            ManagedIdentifier.AppendSpan(_stringBuilder, withoutArity);
        }

        if (arity > 0)
        {
            _stringBuilder.Append('<').Append(',', arity - 1).Append('>');
        }

        if (elementTypeIndex >= 0)
            ManagedIdentifier.AppendSpan(_stringBuilder, TreeNodeFilterHelper.Escape(typeName.AsSpan(elementTypeIndex)));

        _hasSlash = false;
    }

    public void WriteArraySpecifier(int rank = 0)
    {
        if (_hasSlash)
            throw new InvalidOperationException();

        switch (rank)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(rank));
            case 0:
                _stringBuilder.Append("%5B%5D" /* [] */);
                break;
            case 1:
                _stringBuilder.Append("%5B%2A%5D" /* [*] */);
                break;
            default:
                _stringBuilder.Append("%5B" /* [ */);
                for (int i = 1; i < rank; ++i)
                    _stringBuilder.Append("%5D" /* , */);
                _stringBuilder.Append("%5D");
                break;
        }
    }

    public void WritePointerSpecifier()
    {
        if (_hasSlash)
            throw new InvalidOperationException();

        _stringBuilder.Append("%2A" /* * */);
    }

    public void WriteReferenceSpecifier()
    {
        if (_hasSlash)
            throw new InvalidOperationException();

        _stringBuilder.Append("%26" /* & */);
    }

    public void WriteMethodName(string methodName, int arity = 0)
    {
        WriteTypeName(methodName, arity);
        _parameterTypeIndex = 0;
    }

    public void WriteParameterType(string managedIdentifier)
    {
        if (_parameterTypeIndex < 0)
            throw new InvalidOperationException();
        if (_parameterTypeIndex == 0)
            _stringBuilder.Append("%28" /* ( */);
        else
            _stringBuilder.Append(',');

        _stringBuilder.Append(TreeNodeFilterHelper.Escape(managedIdentifier));
        ++_parameterTypeIndex;
        _hasSlash = false;
    }

    public void WriteGenericParameter(string managedIdentifier)
    {
        if (_genericParameterIndex == 0)
        {
            if (!_hasSlash) WriteSeparator();
            _stringBuilder.Append('<');
        }
        else
            _stringBuilder.Append(',');

        _stringBuilder.Append(TreeNodeFilterHelper.Escape(managedIdentifier));
        ++_genericParameterIndex;
        _hasSlash = false;
    }

    public delegate void WriteParameterValueHandler<TState>(ref TState state, StringBuilder stringBuilder);
    public delegate void WriteParameterValueHandler<TState1, TState2>(ref TState1 state, ref TState2 state2, StringBuilder stringBuilder);

    /// <remarks>Raw text written to the string builder needs to be esacped using <see cref="TreeNodeFilterHelper.Escape(string)"/>.</remarks>
    public void WriteParameterValue<TState>(ref TState state, WriteParameterValueHandler<TState> writer)
    {
        if (_parameterIndex == 0)
        {
            if (!_hasSlash) WriteSeparator();
            _stringBuilder.Append("%28" /* ( */);
        }
        else
            _stringBuilder.Append(',');

        writer(ref state, _stringBuilder);
        ++_parameterIndex;
        _hasSlash = false;
    }

    internal void WriteParameterValue<TState1, TState2>(ref TState1 state, ref TState2 writeState, WriteParameterValueHandler<TState1, TState2> writer)
    {
        if (_parameterIndex == 0)
        {
            if (!_hasSlash) WriteSeparator();
            _stringBuilder.Append("%28" /* ( */);
        }
        else
            _stringBuilder.Append(',');

        writer(ref state, ref writeState, _stringBuilder);
        ++_parameterIndex;
        _hasSlash = false;
    }

    public readonly override string ToString() => _stringBuilder.ToString();
    
    public string FlushToString()
    {
        Flush();
        return _stringBuilder.Length == 0 ? "/" : _stringBuilder.ToString();
    }
}