using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using uTest;
using uTest.Runner.Util;
using Assert = NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming

namespace uTest_Test;

public class GetMethodInfoByManagedMethodTests
{
    [NUnit.Framework.Test]
    public void BasicMethod()
    {
        MethodInfo? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), "Method1");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action>(x => x.Method1))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithEmptyParamsSpecifier()
    {
        MethodInfo? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), "Method1()");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action>(x => x.Method1))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWith1Param()
    {
        MethodInfo? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), "Method1(System.Int32)");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<int>>(x => x.Method1))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWith2Params()
    {
        MethodInfo? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), "Method1(System.Int32,System.String)");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<int, string>>(x => x.Method1))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithElementTypes()
    {
        MethodInfo? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), "Method1(System.Int32[],System.String&,System.Void*[,],System.String[]&)");
        
        MethodInfo? byReflection =
            typeof(BasicMethod_Class).GetMethod("Method1", BindingFlags.Public | BindingFlags.Instance, null, [ typeof(int[]), typeof(string).MakeByRefType(), typeof(void*).MakeArrayType(2), typeof(string[]).MakeByRefType() ], null);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter1()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.List`1<System.String>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<List<string>>>(x => x.Method2))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter2()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.Dictionary`2<System.String, System.Int32>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<Dictionary<string, int>>>(x => x.Method2))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter3()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.String>>>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<List<List<List<string>>>>>(x => x.Method2))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter4()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.Dictionary`2<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.String>>,System.Collections.Generic.List`1<System.Int32>>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<Dictionary<List<List<string>>, List<int>>>>(x => x.Method2))
        );
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithTypeOnly_Open()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<>),
            "Method3(!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method3", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithTypeOnly_Constructed()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<string>),
            "Method3(System.String)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method3", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithMethodOnly_Open()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<>),
            "Method4`1(!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method4", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithMethodOnly_Constructed()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<string>),
            "Method4`1(!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method4", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithBoth_Open()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<>),
            "Method5`1(!0,!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method5", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithBoth_Constructed()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<string>),
            "Method5`1(System.String,!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method5", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationDispose()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            "System.IDisposable.Dispose"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.IDisposable.Dispose", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationDisposeWithEmptyParams()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            "System.IDisposable.Dispose()"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.IDisposable.Dispose", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationGetEnumerator()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            "System.Collections.Generic.IEnumerable<System.String>.GetEnumerator"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.Collections.Generic.IEnumerable<System.String>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationGetEnumeratorWithEmptyParams()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            "System.Collections.Generic.IEnumerable<System.String>.GetEnumerator()"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.Collections.Generic.IEnumerable<System.String>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericExplicitImplementationGetEnumerator()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericExplicitImplementations<string>),
            "System.Collections.Generic.IEnumerable<T>.GetEnumerator"
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<string>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericExplicitImplementationGetEnumeratorWithEmptyParams()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(GenericExplicitImplementations<string>),
            "System.Collections.Generic.IEnumerable<T>.GetEnumerator()"
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<string>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void NestedGenerics1()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(A<>.B<>),
            "Method1(!0,!1)"
        );

        MethodInfo? byReflection =
            typeof(A<>.B<>).GetMethod("Method1", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void NestedGenerics2()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(A<>.B<>),
            "Method2(!1)"
        );

        MethodInfo? byReflection =
            typeof(A<>.B<>).GetMethod("Method2", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void NestedGenerics3()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(A<>.B<>),
            "Method3`1(!0, !1, !!0)"
        );

        MethodInfo? byReflection =
            typeof(A<>.B<>).GetMethod("Method3", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void NestedNonGenericTypeInGenericType()
    {
        MethodInfo? method = ManagedIdentifier.FindMethod(
            typeof(List<>.Enumerator),
            "MoveNext"
        );

        MethodInfo? byReflection =
            typeof(List<>.Enumerator).GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    public unsafe class BasicMethod_Class
    {
        public void Method1() { }
        public void Method1(int x) { }
        public void Method1(int x, string y) { }
        public void Method1(int[] x, ref string y, void*[,] arr, ref string[] array2) { }
        public void Method2(List<string> str) { }
        public void Method2(Dictionary<string, int> dict) { }
        public void Method2(List<List<List<string>>> str) { }
        public void Method2(Dictionary<List<List<string>>, List<int>> dict) { }
    }

    public class GenericMethod<T>
    {
        public void Method3(T t) { }
        public void Method4<X>(X x) { }
        public void Method5<X>(T t, X x) { }
    }

    public class BasicExplicitImplementations : IDisposable, IEnumerable<string>
    {
        void IDisposable.Dispose() { }
        IEnumerator<string> IEnumerable<string>.GetEnumerator() => throw null!;
        IEnumerator IEnumerable.GetEnumerator() => throw null!;
    }

    public class GenericExplicitImplementations<T> : IEnumerable<T>
    {
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw null!;
        IEnumerator IEnumerable.GetEnumerator() => throw null!;
    }

    public class A<T>
    {
        public class B<X>
        {
            public void Method1(T t, X x) { }
            public void Method2(X x) { }
            public void Method3<U>(T t, X x, U u) { }
        }
    }
}
