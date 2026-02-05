using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace uTest.Compat.Utility;

/// <summary>
/// Checks for attributes on members using a language-based hierarchical check, so including nesting parent types, inherited types, modules, and assmeblies.
/// <para>
/// Order of relevance: Parameter -&gt; Member -&gt; [ Overridden Members -&gt; ] Declaring Type -&gt; [ Overridden Types -&gt; ] Nesting Types (in to out) -&gt; Module -&gt; Assembly.
/// </para>
/// </summary>
/// <typeparam name="TAttribute">The <see cref="Attribute"/> type or an interface.</typeparam>
public static class TestAttributeHelper<TAttribute> where TAttribute : class
{
    /// <summary>
    /// Invoked by <see cref="TestAttributeHelper{TAttribute}"/> for each attribute on a member.
    /// </summary>
    public delegate void ForEachAttributeCallback<TState>(ref TState state, TAttribute attribute, ICustomAttributeProvider member);

    /// <summary>
    /// Invoked by <see cref="TestAttributeHelper{TAttribute}"/> for each attribute on a member until <see langword="false"/> is returned.
    /// </summary>
    /// <returns><see langword="true"/> to continue to the next attribute, <see langword="false"/> to break the loop.</returns>
    public delegate bool ForEachAttributeWhileCallback<TState>(ref TState state, TAttribute attribute, ICustomAttributeProvider member);

    private static readonly Dictionary<Assembly, object[]> AssemblyCache = new Dictionary<Assembly, object[]>();
    private static readonly Dictionary<Module, object[]> ModuleCache = new Dictionary<Module, object[]>();
    private static readonly Dictionary<Type, object[]> TypeCache = new Dictionary<Type, object[]>();

    private static Dictionary<Type, bool>? _inheritanceCache;

    private static readonly bool IsMaybeInherited;
    private static readonly bool IsInterface;

    private static readonly object Sync;

    /// <summary>
    /// Checks if <typeparamref name="TAttribute"/> is inherited by overriding methods and extending types.
    /// </summary>
    public static bool IsInherited => IsInterface ? throw new InvalidOperationException("Not valid on interfaces.") : IsMaybeInherited;

    static TestAttributeHelper()
    {
        Sync = AssemblyCache;
        Type attributeType = typeof(TAttribute);

        if (!attributeType.IsSubclassOf(typeof(Attribute)))
        {
            if (!attributeType.IsInterface)
                throw new ArgumentException("Type parameter TAttribute should be a subclass of Attribute or an interface.");
            IsInterface = true;
            IsMaybeInherited = true;
            return;
        }

        AttributeUsageAttribute? usage = TestAttributeHelper.GetAttributeSafe<AttributeUsageAttribute>(attributeType);
        IsMaybeInherited = usage?.Inherited ?? true;
    }

    private static bool GetIsInherited(Type attributeType)
    {
        if (IsInterface)
        {
            if (_inheritanceCache == null)
                Interlocked.CompareExchange(ref _inheritanceCache, new Dictionary<Type, bool>(4), null);

            bool isInherited;
            lock (Sync)
            {
                if (!_inheritanceCache.TryGetValue(attributeType, out isInherited))
                {
                    isInherited = IsInheritedCore(attributeType);
                    _inheritanceCache.Add(attributeType, isInherited);
                }
            }

            return isInherited;
        }

        // this code path shouldn't really be executed
        if (typeof(TAttribute) == attributeType)
        {
            return IsMaybeInherited;
        }

        return IsInheritedCore(attributeType);

        static bool IsInheritedCore(Type attributeType)
        {
            AttributeUsageAttribute? usage = TestAttributeHelper.GetAttributeSafe<AttributeUsageAttribute>(attributeType);
            return usage?.Inherited ?? true;
        }
    }

    /// <summary>
    /// Check whether or not the attribute type is present on a member or any of it's syntaxical parents.
    /// </summary>
    /// <param name="member">The member to check.</param>
    /// <param name="inherit">Whether or not parnet types and overridden members should be checked.</param>
    public static bool IsDefined(ICustomAttributeProvider member, bool inherit = true)
    {
        switch (member)
        {
            case Assembly asm:
                return IsDefinedIntl(asm);

            case Module mod:
                if (IsDefinedIntl(mod))
                    return true;
                
                return IsDefinedIntl(mod.Assembly);

            case Type type:
                if (IsDefinedIntl(type, inherit))
                    return true;
                if (IsDefinedIntl(type.Module))
                    return true;

                return IsDefinedIntl(type.Assembly);
        }

        ICustomAttributeProvider? m = member;
        while (m != null)
        {
            try
            {
                if (m.IsDefined(typeof(TAttribute), inherit))
                    return true;
            }
            catch
            {
                // ignored
            }

            if (m is ParameterInfo p)
                m = p.Member;
            else
                break;
        }

        for (Type? t = (m as MemberInfo)?.DeclaringType; t != null; t = t.DeclaringType)
        {
            if (IsDefinedIntl(t, inherit))
                return true;
            
            if (t.IsNested)
                continue;

            if (t.Module.IsDefined(typeof(TAttribute)) || t.Assembly.IsDefined(typeof(TAttribute)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the most relevant instance of the attribute type on a member or any of it's syntaxical parents.
    /// </summary>
    /// <remarks>
    /// The 'most relevant' attribute is the one on the least significant member.
    /// For example, if an attribute is on both a method and it's containing class, the one on the method will be returned.
    /// </remarks>
    /// <param name="member">The member to check.</param>
    /// <param name="inherit">Whether or not parnet types and overridden members should be checked.</param>
    public static TAttribute? GetAttribute(ICustomAttributeProvider member, bool inherit = true)
    {
        TAttribute? tempAttr;
        switch (member)
        {
            case Assembly asm:
                return GetAttributeIntl(asm);

            case Module mod:
                tempAttr = GetAttributeIntl(mod);
                if (tempAttr != null)
                    return tempAttr;
                return GetAttributeIntl(mod.Assembly);

            case Type type:
                tempAttr = GetAttributeIntl(type, inherit);
                if (tempAttr != null)
                    return tempAttr;
                tempAttr = GetAttributeIntl(type.Module);
                if (tempAttr != null)
                    return tempAttr;
                return GetAttributeIntl(type.Assembly);
        }

        ICustomAttributeProvider? m = member;
        while (m != null)
        {
            try
            {
                switch (m)
                {
                    case MemberInfo memberInfo:
                        Attribute? a = memberInfo.GetCustomAttribute(typeof(TAttribute), inherit);
                        if (a is TAttribute t)
                            return t;
                        break;

                    case ParameterInfo parameterInfo:
                        a = parameterInfo.GetCustomAttribute(typeof(TAttribute), inherit);
                        if (a is TAttribute t2)
                            return t2;
                        break;
                }
            }
            catch
            {
                // ignored
            }

            if (m is ParameterInfo p)
                m = p.Member;
            else
                break;
        }

        for (Type? t = (m as MemberInfo)?.DeclaringType; t != null; t = t.DeclaringType)
        {
            tempAttr = GetAttributeIntl(t, inherit);
            if (tempAttr != null)
                return tempAttr;

            if (t.IsNested)
                continue;

            tempAttr = GetAttributeIntl(t.Module);
            if (tempAttr != null)
                return tempAttr;
            tempAttr = GetAttributeIntl(t.Assembly);
            if (tempAttr != null)
                return tempAttr;
        }

        return null;
    }

    /// <summary>
    /// Invokes a callback on all instances of the attribute type on a member or any of it's syntaxical parents, in order of relevance.
    /// </summary>
    /// <remarks>
    /// The 'most relevant' attribute is the one on the least significant member.
    /// For example, if an attribute is on both a method and it's containing class, the one on the method will be returned.
    /// </remarks>
    /// <param name="state">Arbitrary value to pass to the callback.</param>
    /// <param name="member">The member to check.</param>
    /// <param name="callback">Function to invoke for each available attribute.</param>
    /// <param name="inherit">Whether or not parnet types and overridden members should be checked.</param>
    public static void ForEachAttribute<TState>(ref TState state, ICustomAttributeProvider member, ForEachAttributeCallback<TState> callback, bool inherit = true)
    {
        switch (member)
        {
            case Assembly asm:
                ForEachAttributeIn(asm, callback, ref state);
                return;

            case Module mod:
                ForEachAttributeIn(mod, callback, ref state);
                ForEachAttributeIn(mod.Assembly, callback, ref state);
                return;

            case Type type:
                ForEachAttributeIn(type, callback, ref state, inherit);
                ForEachAttributeIn(type.Module, callback, ref state);
                ForEachAttributeIn(type.Assembly, callback, ref state);
                break;
        }

        ICustomAttributeProvider? m = member;
        while (m != null)
        {
            object[] objs;
            try
            {
                objs = m.GetCustomAttributes(typeof(TAttribute), inherit);
            }
            catch
            {
                objs = Array.Empty<object>();
            }

            for (int i = 0; i < objs.Length; ++i)
            {
                callback(ref state, (TAttribute)objs[i], m);
            }

            if (m is ParameterInfo p)
                m = p.Member;
            else
                break;
        }

        for (Type? t = (m as MemberInfo)?.DeclaringType; t != null; t = t.DeclaringType)
        {
            ForEachAttributeIn(t, callback, ref state, inherit);
            if (t.IsNested)
                continue;
            ForEachAttributeIn(t.Module, callback, ref state);
            ForEachAttributeIn(t.Assembly, callback, ref state);
        }
    }

    /// <summary>
    /// Invokes a callback on all instances of the attribute type on a member or any of it's syntaxical parents, in order of relevance.
    /// <para>
    /// The loop will continue while the callback returns <see langword="true"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The 'most relevant' attribute is the one on the least significant member.
    /// For example, if an attribute is on both a method and it's containing class, the one on the method will be returned.
    /// </remarks>
    /// <param name="state">Arbitrary value to pass to the callback.</param>
    /// <param name="member">The member to check.</param>
    /// <param name="callback">Function to invoke for each available attribute.</param>
    /// <param name="inherit">Whether or not parnet types and overridden members should be checked.</param>
    public static bool ForEachAttributeWhile<TState>(ref TState state, ICustomAttributeProvider member, ForEachAttributeWhileCallback<TState> callback, bool inherit = true)
    {
        switch (member)
        {
            case Assembly asm:
                return !ForEachAttributeIn(asm, callback, ref state);

            case Module mod:
                if (!ForEachAttributeIn(mod, callback, ref state))
                    return true;
                return !ForEachAttributeIn(mod.Assembly, callback, ref state);

            case Type type:
                if (!ForEachAttributeIn(type, callback, ref state, inherit))
                    return true;
                if (!ForEachAttributeIn(type.Module, callback, ref state))
                    return true;
                return !ForEachAttributeIn(type.Assembly, callback, ref state);
        }

        ICustomAttributeProvider? m = member;
        while (m != null)
        {
            object[] objs;
            try
            {
                objs = m.GetCustomAttributes(typeof(TAttribute), inherit);
            }
            catch
            {
                objs = Array.Empty<object>();
            }

            for (int i = 0; i < objs.Length; ++i)
            {
                if (!callback(ref state, (TAttribute)objs[i], m))
                    return true;
            }

            if (m is ParameterInfo p)
                m = p.Member;
            else
                break;
        }

        for (Type? t = (m as MemberInfo)?.DeclaringType; t != null; t = t.DeclaringType)
        {
            if (!ForEachAttributeIn(t, callback, ref state, inherit))
                return true;
            if (t.IsNested)
                continue;
            if (!ForEachAttributeIn(t.Module, callback, ref state))
                return true;
            if (!ForEachAttributeIn(t.Assembly, callback, ref state))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds all instances of the attribute type on a member or any of it's syntaxical parents to an <see cref="IList{T}"/>, in order of relevance.
    /// </summary>
    /// <remarks>
    /// The 'most relevant' attribute is the one on the least significant member.
    /// For example, if an attribute is on both a method and it's containing class, the one on the method will be returned.
    /// </remarks>
    /// <param name="member">The member to check.</param>
    /// <param name="output">List to add attribute instances to.</param>
    /// <param name="inherit">Whether or not parnet types and overridden members should be checked.</param>
    public static void GetAttributes(ICustomAttributeProvider member, IList<TAttribute> output, bool inherit = true)
    {
        switch (member)
        {
            case Assembly asm:
                GetAssemblyAttributes(asm, output);
                return;

            case Module mod:
                GetModuleAttributes(mod, output);
                GetAssemblyAttributes(mod.Assembly, output);
                return;

            case Type type:
                GetTypeAttributes(type, output, inherit);
                GetModuleAttributes(type.Module, output);
                GetAssemblyAttributes(type.Assembly, output);
                break;
        }

        ICustomAttributeProvider? m = member;
        while (m != null)
        {
            object[] objs;
            try
            {
                objs = m.GetCustomAttributes(typeof(TAttribute), inherit);
            }
            catch
            {
                objs = Array.Empty<object>();
            }

            for (int i = 0; i < objs.Length; ++i)
            {
                output.Add((TAttribute)objs[i]);
            }
            
            if (m is ParameterInfo p)
                m = p.Member;
            else
                break;
        }

        for (Type? t = (m as MemberInfo)?.DeclaringType; t != null; t = t.DeclaringType)
        {
            GetTypeAttributes(t, output, inherit);
            if (t.IsNested)
                continue;
            GetModuleAttributes(t.Module, output);
            GetAssemblyAttributes(t.Assembly, output);
        }
    }

    private static void GetAssemblyAttributes(Assembly asm, IList<TAttribute> output)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!AssemblyCache.TryGetValue(asm, out attributes))
            {
                try
                {
                    attributes = asm.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                AssemblyCache.Add(asm, attributes);
            }
        }

        for (int i = 0; i < attributes.Length; ++i)
        {
            output.Add((TAttribute)attributes[i]);
        }
    }

    private static void ForEachAttributeIn<TState>(Assembly asm, ForEachAttributeCallback<TState> callback, ref TState state)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!AssemblyCache.TryGetValue(asm, out attributes))
            {
                try
                {
                    attributes = asm.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                AssemblyCache.Add(asm, attributes);
            }
        }

        for (int i = 0; i < attributes.Length; ++i)
        {
            callback(ref state, (TAttribute)attributes[i], asm);
        }
    }

    private static bool ForEachAttributeIn<TState>(Assembly asm, ForEachAttributeWhileCallback<TState> callback, ref TState state)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!AssemblyCache.TryGetValue(asm, out attributes))
            {
                try
                {
                    attributes = asm.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                AssemblyCache.Add(asm, attributes);
            }
        }

        for (int i = 0; i < attributes.Length; ++i)
        {
            if (!callback(ref state, (TAttribute)attributes[i], asm))
                return false;
        }

        return true;
    }

    private static bool IsDefinedIntl(Assembly asm)
    {
        bool def;
        object[]? attributes;
        lock (Sync)
        {
            def = AssemblyCache.TryGetValue(asm, out attributes);
        }

        if (!def)
        {
            try
            {
                def = asm.IsDefined(typeof(TAttribute), false);
            }
            catch
            {
                def = false;
            }

            if (!def)
            {
                lock (Sync)
                    AssemblyCache[asm] = Array.Empty<object>();
            }

            return def;
        }

        return attributes!.Length > 0;
    }

    private static TAttribute? GetAttributeIntl(Assembly asm)
    {
        bool def;
        object[]? attributes;
        lock (Sync)
        {
            def = AssemblyCache.TryGetValue(asm, out attributes);
        }

        if (!def)
        {
            TAttribute? attribute;
            try
            {
                attribute = (TAttribute?)(object?)asm.GetCustomAttribute(typeof(TAttribute));
            }
            catch
            {
                attribute = null;
            }

            if (attribute == null)
            {
                lock (Sync)
                    AssemblyCache[asm] = Array.Empty<object>();
            }

            return attribute;
        }

        for (int i = 0; i < attributes!.Length; ++i)
        {
            if (attributes[i] is TAttribute attr)
                return attr;
        }

        return null;
    }

    private static void GetModuleAttributes(Module module, IList<TAttribute> output)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!ModuleCache.TryGetValue(module, out attributes))
            {
                try
                {
                    attributes = module.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                ModuleCache.Add(module, attributes);
            }
        }

        for (int i = 0; i < attributes.Length; ++i)
        {
            output.Add((TAttribute)attributes[i]);
        }
    }

    private static void ForEachAttributeIn<TState>(Module module, ForEachAttributeCallback<TState> callback, ref TState state)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!ModuleCache.TryGetValue(module, out attributes))
            {
                try
                {
                    attributes = module.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                ModuleCache.Add(module, attributes);
            }
        }

        for (int i = 0; i < attributes.Length; ++i)
        {
            callback(ref state, (TAttribute)attributes[i], module);
        }
    }

    private static bool ForEachAttributeIn<TState>(Module module, ForEachAttributeWhileCallback<TState> callback, ref TState state)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!ModuleCache.TryGetValue(module, out attributes))
            {
                try
                {
                    attributes = module.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                ModuleCache.Add(module, attributes);
            }
        }

        for (int i = 0; i < attributes.Length; ++i)
        {
            if (!callback(ref state, (TAttribute)attributes[i], module))
                return false;
        }

        return true;
    }

    private static bool IsDefinedIntl(Module module)
    {
        bool def;
        object[]? attributes;
        lock (Sync)
        {
            def = ModuleCache.TryGetValue(module, out attributes);
        }

        if (!def)
        {
            try
            {
                def = module.IsDefined(typeof(TAttribute), false);
            }
            catch
            {
                def = false;
            }

            if (!def)
            {
                lock (Sync)
                    ModuleCache[module] = Array.Empty<object>();
            }

            return def;
        }

        return attributes!.Length > 0;
    }

    private static TAttribute? GetAttributeIntl(Module module)
    {
        bool def;
        object[]? attributes;
        lock (Sync)
        {
            def = ModuleCache.TryGetValue(module, out attributes);
        }

        if (!def)
        {
            TAttribute? attribute;
            try
            {
                attribute = (TAttribute?)(object?)module.GetCustomAttribute(typeof(TAttribute));
            }
            catch
            {
                attribute = null;
            }

            if (attribute == null)
            {
                lock (Sync)
                    ModuleCache[module] = Array.Empty<object>();
            }

            return attribute;
        }

        for (int i = 0; i < attributes!.Length; ++i)
        {
            if (attributes[i] is TAttribute attr)
                return attr;
        }

        return null;
    }

    private static void GetTypeAttributes(Type type, IList<TAttribute> output, bool inherit)
    {
        GetTypeAttributesState state;
        state.List = output;
        ForEachAttributeIn(type, static (ref state, attribute, _) =>
        {
            state.List.Add(attribute);
        }, ref state, inherit);
    }

    private struct GetTypeAttributesState
    {
        public IList<TAttribute> List;
    }

    private static void ForEachAttributeIn<TState>(Type type, ForEachAttributeCallback<TState> callback, ref TState state, bool inherit)
    {
        if (inherit && IsMaybeInherited)
        {
            bool isTop = true;
            for (Type? t = type; t != null; t = t.BaseType)
            {
                object[] attributes = GetAttributesNoInheritance(t);

                if (!isTop)
                {
                    for (int i = 0; i < attributes.Length; ++i)
                    {
                        if (IsInterface)
                        {
                            Type attrType = attributes[i].GetType();
                            if (!GetIsInherited(attrType))
                                continue;
                        }

                        callback(ref state, (TAttribute)attributes[i], t);
                    }
                }
                else
                {
                    for (int i = 0; i < attributes.Length; ++i)
                    {
                        callback(ref state, (TAttribute)attributes[i], t);
                    }
                    isTop = false;
                }
            }
        }
        else
        {
            object[] attributes = GetAttributesNoInheritance(type);
            for (int i = 0; i < attributes.Length; ++i)
            {
                callback(ref state, (TAttribute)attributes[i], type);
            }
        }
    }

    private static object[] GetAttributesNoInheritance(Type type)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!TypeCache.TryGetValue(type, out attributes))
            {
                try
                {
                    attributes = type.GetCustomAttributes(typeof(TAttribute), false);
                }
                catch
                {
                    attributes = Array.Empty<object>();
                }

                TypeCache.Add(type, attributes);
            }
        }

        return attributes;
    }
    private static bool GetIsDefinedNoInheritance(Type type)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!TypeCache.TryGetValue(type, out attributes))
            {
                bool isDefined;
                try
                {
                    isDefined = type.IsDefined(typeof(TAttribute), false);
                }
                catch
                {
                    isDefined = false;
                }

                if (!isDefined)
                    TypeCache.Add(type, Array.Empty<object>());
                
                return isDefined;
            }
        }

        return attributes.Length > 0;
    }
    private static TAttribute? GetAttributeNoInheritance(Type type)
    {
        object[] attributes;
        lock (Sync)
        {
            if (!TypeCache.TryGetValue(type, out attributes))
            {
                TAttribute? attribute;
                try
                {
                    attribute = (TAttribute?)(object?)type.GetCustomAttribute(typeof(TAttribute), false);
                }
                catch
                {
                    attribute = null;
                }

                if (attribute == null)
                    TypeCache.Add(type, Array.Empty<object>());
                
                return attribute;
            }
        }

        return attributes.Length == 0 ? null : (TAttribute?)attributes[0];
    }

    private static bool ForEachAttributeIn<TState>(Type type, ForEachAttributeWhileCallback<TState> callback, ref TState state, bool inherit)
    {
        if (inherit && IsMaybeInherited)
        {
            bool isTop = true;
            for (Type? t = type; t != null; t = t.BaseType)
            {
                object[] attributes = GetAttributesNoInheritance(t);

                if (!isTop)
                {
                    for (int i = 0; i < attributes.Length; ++i)
                    {
                        if (IsInterface)
                        {
                            Type attrType = attributes[i].GetType();
                            if (!GetIsInherited(attrType))
                                continue;
                        }

                        if (!callback(ref state, (TAttribute)attributes[i], t))
                            return true;
                    }
                }
                else
                {
                    for (int i = 0; i < attributes.Length; ++i)
                    {
                        if (!callback(ref state, (TAttribute)attributes[i], t))
                            return true;
                    }
                    isTop = false;
                }
            }
        }
        else
        {
            object[] attributes = GetAttributesNoInheritance(type);
            for (int i = 0; i < attributes.Length; ++i)
            {
                if (!callback(ref state, (TAttribute)attributes[i], type))
                    return true;
            }
        }

        return false;
    }

    private static bool IsDefinedIntl(Type type, bool inherit)
    {
        if (!IsMaybeInherited || !inherit)
        {
            return GetIsDefinedNoInheritance(type);
        }

        bool isTop = true;
        for (Type? t = type; t != null; t = t.BaseType)
        {
            if (isTop)
            {
                bool isDefined = GetIsDefinedNoInheritance(t);
                if (isDefined)
                    return true;
                continue;
            }

            isTop = false;

            if (!IsInterface)
            {
                if (GetIsDefinedNoInheritance(t))
                    return true;

                continue;
            }

            object[] attributes = GetAttributesNoInheritance(t);
            for (int i = 0; i < attributes.Length; ++i)
            {
                Type attrType = attributes[i].GetType();
                if (GetIsInherited(attrType))
                    return true;
            }
        }

        return false;
    }

    private static TAttribute? GetAttributeIntl(Type type, bool inherit)
    {
        if (!inherit || !IsMaybeInherited)
        {
            return GetAttributeNoInheritance(type);
        }

        bool isTop = true;
        for (Type? t = type; t != null; t = t.BaseType)
        {
            if (isTop)
            {
                TAttribute? attribute = GetAttributeNoInheritance(t);
                if (attribute != null)
                    return attribute;
                continue;
            }

            isTop = false;

            if (!IsInterface)
            {
                TAttribute? attribute = GetAttributeNoInheritance(t);
                if (attribute != null)
                    return attribute;

                continue;
            }

            object[] attributes = GetAttributesNoInheritance(t);
            for (int i = 0; i < attributes.Length; ++i)
            {
                Type attrType = attributes[i].GetType();
                if (GetIsInherited(attrType))
                    return (TAttribute?)attributes[i];
            }
        }

        return null;
    }
}

file static class TestAttributeHelper
{
    internal static TAttribute? GetAttributeSafe<TAttribute>(ICustomAttributeProvider member, bool inherit = false) where TAttribute : Attribute
    {
        try
        {
            switch (member)
            {
                case MemberInfo memberInfo:
                    return (TAttribute?)Attribute.GetCustomAttribute(memberInfo, typeof(TAttribute), inherit);
                case Module module:
                    return (TAttribute?)Attribute.GetCustomAttribute(module, typeof(TAttribute), inherit);
                case Assembly assembly:
                    return (TAttribute?)Attribute.GetCustomAttribute(assembly, typeof(TAttribute), inherit);
                case ParameterInfo parameterInfo:
                    return (TAttribute?)Attribute.GetCustomAttribute(parameterInfo, typeof(TAttribute), inherit);
                default:
                    object[] attributes = member.GetCustomAttributes(typeof(TAttribute), inherit);
                    if (attributes is not { Length: > 0 })
                        return null;
                    if (attributes.Length > 1)
                        throw new AmbiguousMatchException($"Multiple attributes of type {typeof(TAttribute).FullName}.");

                    return (TAttribute?)attributes[0];
            }
        }
        catch (TypeLoadException) { }
        catch (FileNotFoundException) { }

        return null;
    }
}