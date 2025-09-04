using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using uTest.Runner.Util;

// ReSharper disable InconsistentNaming

namespace uTest_Test;

public class GetMethodInfoByManagedMethodTests
{
    [Test]
    public void BasicMethod()
    {
        MethodInfo method
            = SourceGenerationServices.GetMethodInfoByManagedMethod(typeof(BasicMethod_Class), "Method1");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action>(x => x.Method1))
        );
    }

    [Test]
    public void BasicMethodWithEmptyParamsSpecifier()
    {
        MethodInfo method
            = SourceGenerationServices.GetMethodInfoByManagedMethod(typeof(BasicMethod_Class), "Method1()");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action>(x => x.Method1))
        );
    }

    [Test]
    public void BasicMethodWith1Param()
    {
        MethodInfo method
            = SourceGenerationServices.GetMethodInfoByManagedMethod(typeof(BasicMethod_Class), "Method1(System.Int32)");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<int>>(x => x.Method1))
        );
    }

    [Test]
    public void BasicMethodWith2Params()
    {
        MethodInfo method
            = SourceGenerationServices.GetMethodInfoByManagedMethod(typeof(BasicMethod_Class), "Method1(System.Int32,System.String)");

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<int, string>>(x => x.Method1))
        );
    }

    [Test]
    public void BasicMethodWithGenericParameter1()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.List`1<System.String>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<List<string>>>(x => x.Method2))
        );
    }

    [Test]
    public void BasicMethodWithGenericParameter2()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.Dictionary`2<System.String, System.Int32>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<Dictionary<string, int>>>(x => x.Method2))
        );
    }

    [Test]
    public void BasicMethodWithGenericParameter3()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.String>>>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<List<List<List<string>>>>>(x => x.Method2))
        );
    }

    [Test]
    public void BasicMethodWithGenericParameter4()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicMethod_Class),
            "Method2(System.Collections.Generic.Dictionary`2<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.String>>,System.Collections.Generic.List`1<System.Int32>>)"
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<Dictionary<List<List<string>>, List<int>>>>(x => x.Method2))
        );
    }

    [Test]
    public void GenericMethodWithTypeOnly_Open()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericMethod<>),
            "Method3(!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method3", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericMethodWithTypeOnly_Constructed()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericMethod<string>),
            "Method3(System.String)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method3", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericMethodWithMethodOnly_Open()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericMethod<>),
            "Method4`1(!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method4", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericMethodWithMethodOnly_Constructed()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericMethod<string>),
            "Method4`1(!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method4", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericMethodWithBoth_Open()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericMethod<>),
            "Method5`1(!0,!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method5", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericMethodWithBoth_Constructed()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericMethod<string>),
            "Method5`1(System.String,!!0)"
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method5", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void BasicExplicitImplementationDispose()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicExplicitImplementations),
            "System.IDisposable.Dispose"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.IDisposable.Dispose", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void BasicExplicitImplementationDisposeWithEmptyParams()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicExplicitImplementations),
            "System.IDisposable.Dispose()"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.IDisposable.Dispose", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void BasicExplicitImplementationGetEnumerator()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicExplicitImplementations),
            "System.Collections.Generic.IEnumerable<System.String>.GetEnumerator"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.Collections.Generic.IEnumerable<System.String>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void BasicExplicitImplementationGetEnumeratorWithEmptyParams()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(BasicExplicitImplementations),
            "System.Collections.Generic.IEnumerable<System.String>.GetEnumerator()"
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.Collections.Generic.IEnumerable<System.String>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericExplicitImplementationGetEnumerator()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericExplicitImplementations<string>),
            "System.Collections.Generic.IEnumerable<T>.GetEnumerator"
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<string>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [Test]
    public void GenericExplicitImplementationGetEnumeratorWithEmptyParams()
    {
        MethodInfo method = SourceGenerationServices.GetMethodInfoByManagedMethod(
            typeof(GenericExplicitImplementations<string>),
            "System.Collections.Generic.IEnumerable<T>.GetEnumerator()"
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<string>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    public class BasicMethod_Class
    {
        public void Method1() { }
        public void Method1(int x) { }
        public void Method1(int x, string y) { }
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
}
