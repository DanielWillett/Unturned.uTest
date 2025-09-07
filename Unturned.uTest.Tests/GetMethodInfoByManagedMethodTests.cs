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
    [TestCase("System.Collections.Generic.IEnumerable<System.String>", new string[] { "System", "Collections", "Generic", "IEnumerable<System.String>" })]
    [TestCase("System", new string[] { "System" })]
    [TestCase(".System.", new string[] { "", "System", "" })]
    [TestCase("System.", new string[] { "System", "" })]
    [TestCase(".System", new string[] { "", "System" })]
    [TestCase("", new string[0])]
    public void SplitExplicitlyImplementedMethodName(string split, string[] values)
    {
        int ct = ManagedIdentifier.Count(split, '.') + 1;

        Span<Range> ranges = stackalloc Range[ct];
        ct = ManagedIdentifier.SplitExplicitlyImplementedMethodName(split, ranges);

        Assert.That(ct, Is.EqualTo(values.Length));

        for (int i = 0; i < ct; ++i)
        {
            Assert.That(split.AsSpan()[ranges[i]].ToString(), Is.EqualTo(values[i]));
        }
    }

    [NUnit.Framework.Test]
    public void BasicMethod()
    {
        const string managedMethod = "Method1";

        MethodBase? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), managedMethod);

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action>(x => x.Method1))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithEmptyParamsSpecifier()
    {
        const string managedMethod = "Method1()";

        MethodBase? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), managedMethod);

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action>(x => x.Method1))
        );
    }

    [NUnit.Framework.Test]
    public void BasicMethodWith1Param()
    {
        const string managedMethod = "Method1(System.Int32)";

        MethodBase? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), managedMethod);

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<int>>(x => x.Method1))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWith2Params()
    {
        const string managedMethod = "Method1(System.Int32,System.String)";

        MethodBase? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), managedMethod);

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<int, string>>(x => x.Method1))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithElementTypes()
    {
        const string managedMethod = "Method1(System.Int32[],System.String&,System.Void*[,],System.String[]&)";

        MethodBase? method
            = ManagedIdentifier.FindMethod(typeof(BasicMethod_Class), managedMethod);
         
        MethodInfo? byReflection =
            typeof(BasicMethod_Class).GetMethod("Method1", BindingFlags.Public | BindingFlags.Instance, null, [ typeof(int[]), typeof(string).MakeByRefType(), typeof(void*).MakeArrayType(2), typeof(string[]).MakeByRefType() ], null);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter1()
    {
        const string managedMethod = "Method2(System.Collections.Generic.List`1<System.String>)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            managedMethod
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<List<string>>>(x => x.Method2))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter2()
    {
        const string managedMethod = "Method2(System.Collections.Generic.Dictionary`2<System.String,System.Int32>)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            managedMethod
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<Dictionary<string, int>>>(x => x.Method2))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter3()
    {
        const string managedMethod = "Method2(System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.String>>>)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            managedMethod
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<List<List<List<string>>>>>(x => x.Method2))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicMethodWithGenericParameter4()
    {
        const string managedMethod = "Method2(System.Collections.Generic.Dictionary`2<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.String>>,System.Collections.Generic.List`1<System.Int32>>)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicMethod_Class),
            managedMethod
        );

        Assert.That(
            method,
            Is.EqualTo(SourceGenerationServices.GetMethodByExpression<BasicMethod_Class, Action<Dictionary<List<List<string>>, List<int>>>>(x => x.Method2))
        );

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithTypeOnly_Open()
    {
        const string managedMethod = "Method3(!0)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method3", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithTypeOnly_Constructed()
    {
        const string managedMethod = "Method3(System.String)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<string>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method3", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithMethodOnly_Open()
    {
        const string managedMethod = "Method4`1(!!0)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method4", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithMethodOnly_Constructed()
    {
        const string managedMethod = "Method4`1(!!0)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<string>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method4", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithBoth_Open()
    {
        const string managedMethod = "Method5`1(!0,!!0)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<>).GetMethod("Method5", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericMethodWithBoth_Constructed()
    {
        const string managedMethod = "Method5`1(System.String,!!0)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericMethod<string>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericMethod<string>).GetMethod("Method5", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationDispose()
    {
        const string managedMethod = "System.IDisposable.Dispose";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.IDisposable.Dispose", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationDisposeWithEmptyParams()
    {
        const string managedMethod = "System.IDisposable.Dispose()";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.IDisposable.Dispose", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationGetEnumerator()
    {
        const string managedMethod = "System.Collections.Generic.IEnumerable<System.String>.GetEnumerator";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.Collections.Generic.IEnumerable<System.String>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void BasicExplicitImplementationGetEnumeratorWithEmptyParams()
    {
        const string managedMethod = "System.Collections.Generic.IEnumerable<System.String>.GetEnumerator()";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(BasicExplicitImplementations),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(BasicExplicitImplementations).GetMethod("System.Collections.Generic.IEnumerable<System.String>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericExplicitImplementationGetEnumerator()
    {
        const string managedMethod = "System.Collections.Generic.IEnumerable<T>.GetEnumerator";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericExplicitImplementations<string>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<string>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericExplicitImplementationGetEnumeratorWithEmptyParams()
    {
        const string managedMethod = "System.Collections.Generic.IEnumerable<T>.GetEnumerator()";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericExplicitImplementations<string>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<string>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void GenericExplicitImplementationGetEnumerator_Open()
    {
        const string managedMethod = "System.Collections.Generic.IEnumerable<T>.GetEnumerator";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericExplicitImplementations<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericExplicitImplementationGetEnumeratorWithEmptyParams_Open()
    {
        const string managedMethod = "System.Collections.Generic.IEnumerable<T>.GetEnumerator()";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(GenericExplicitImplementations<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(GenericExplicitImplementations<>).GetMethod("System.Collections.Generic.IEnumerable<T>.GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));
    }

    [NUnit.Framework.Test]
    public void NestedGenerics1()
    {
        const string managedMethod = "Method1(!0,!1)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(A<>.B<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(A<>.B<>).GetMethod("Method1", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void NestedGenerics2()
    {
        const string managedMethod = "Method2(!1)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(A<>.B<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(A<>.B<>).GetMethod("Method2", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void NestedGenerics3()
    {
        const string managedMethod = "Method3`1(!0,!1,!!0)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(A<>.B<>),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(A<>.B<>).GetMethod("Method3", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void NestedNonGenericTypeInGenericType()
    {
        const string managedMethod = "MoveNext";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(List<>.Enumerator),
            managedMethod
        );

        MethodInfo? byReflection =
            typeof(List<>.Enumerator).GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void InstanceConstructor()
    {
        const string managedMethod = ".ctor(System.Char[])";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(string),
            managedMethod
        );

        MethodBase? byReflection =
            typeof(string).GetConstructor([ typeof(char[]) ]);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void StaticConstructor()
    {
        const string managedMethod = ".cctor";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(Comparer),
            managedMethod
        );

        MethodBase? byReflection =
            typeof(Comparer).GetConstructor(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    public void GenericElementTypes()
    {
        const string managedMethod = "TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[],!0*)";

        MethodBase? method = ManagedIdentifier.FindMethod(
            typeof(Tests<>),
            managedMethod
        );

        MethodBase? byReflection =
            typeof(Tests<>).GetMethod("TestGeneric", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(byReflection, Is.Not.Null);

        Assert.That(method, Is.EqualTo(byReflection));

        Assert.That(ManagedIdentifier.GetManagedMethod(method!), Is.EqualTo(managedMethod));
    }

    [NUnit.Framework.Test]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[,],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[*],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!0[],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!!0>,!!0[],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!1>,!!0[],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[],!1*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[],!0&)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[],!0)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0,!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<System.String>,!!0[],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!1>,!!0[],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[],!0*&)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[]&,!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[,][],!0*)")]
    [TestCase("TestGeneric`1(System.Collections.Generic.List`1<!0>,!!0[][,],!0*)")]
    public void GenericElementTypesDifferentCantMatch(string managedMethod)
    {
        MethodBase? method = ManagedIdentifier.FindMethod(typeof(Tests<>), managedMethod);

        Assert.That(method, Is.Null);
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
    public class Tests<T>
    {
        public unsafe void TestGeneric<T2>(List<T> t1, T2[] t2, T* ptr)
        {

        }
    }
}