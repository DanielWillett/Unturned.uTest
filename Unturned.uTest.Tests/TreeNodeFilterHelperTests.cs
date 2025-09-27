using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web;
using uTest;
using Assert = NUnit.Framework.Assert;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedTypeParameter
// ReSharper disable MemberCanBeMadeStatic.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace uTest_Test;
internal class TreeNodeFilterHelperTests
{
    [NUnit.Framework.Test]
    [TestCase(typeof(decimal), "/{0}/System/Decimal/**")]
    [TestCase(typeof(object), "/{0}/System/Object/**")]
    [TestCase(typeof(Environment.SpecialFolder), "/{0}/System/Environment%2BSpecialFolder/**")]
    [TestCase(typeof(Nested1<>.Nested2<,>.Nested3), "/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests%2BNested1<>%2BNested2<,>%2BNested3/**")]
    [TestCase(typeof(Nested1<int>.Nested2<string, long>.Nested3), "/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests%2BNested1<>%2BNested2<,>%2BNested3/*/<System.Int32,System.String,System.Int64>/**")]
    [TestCase(typeof(string), "/{0}/System/String/**")]
    [TestCase(typeof(int[]), "/{0}/System/Int32%5B%5D/**")]
    [TestCase(typeof(int[][,]), "/{0}/System/Int32%5B%2C%5D%5B%5D/**")]
    [TestCase(typeof(int[][,]*[,,,][]*), "/{0}/System/Int32%5B%2C%5D%5B%5D%2A%5B%5D%5B%2C%2C%2C%5D%2A/**")]
    [TestCase(typeof(Version), "/{0}/System/Version/**")]
    [TestCase(typeof(KeyValuePair<string, int[,][]>), "/{0}/System.Collections.Generic/KeyValuePair<,>/*/<System.String,System.Int32%5B%5D%5B%2C%5D>/**")]
    public void TypeAsExpected(Type type, string expected)
    {
        // mscorlib is different on different platforms
        expected = string.Format(expected, typeof(object).Assembly.GetName().Name);
        
        string name = TreeNodeFilterHelper.GetTypeFilter(type);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo(expected));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    private class Nested1<T>
    {
        public class Nested2<T1, T2>
        {
            public class Nested3
            {

            }
        }
    }

    [NUnit.Framework.Test, MethodImpl(MethodImplOptions.NoInlining)]
    public void BasicMethodInNonGenericType()
    {
        MethodInfo method = (MethodInfo)MethodInfo.GetCurrentMethod()!;

        string name = TreeNodeFilterHelper.GetMethodFilter(method);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/BasicMethodInNonGenericType"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    private void TestMethod1(string str) { }

    [NUnit.Framework.Test]
    public void BasicMethodWithParametersInNonGenericType()
    {
        MethodInfo method = new Action<string>(TestMethod1).Method;

        string name = TreeNodeFilterHelper.GetMethodFilter(method, ref method, (parameter, ref state, builder) =>
        {
            builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/TestMethod1%28System.String%29/%28\"Value\"%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    private void TestMethod2<T>() { }

    [NUnit.Framework.Test]
    public void OpenGenericMethodInNonGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTests)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/TestMethod2<>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    private void TestMethod3<T>(string str, int i) { }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithOpenParametersInNonGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTests)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/TestMethod3<>%28System.String,System.Int32%29/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithParametersInNonGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTests)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!, ref method, (parameter, ref state, builder) =>
        {
            switch (parameter.Position)
            {
                case 0:
                    builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
                    break;
                case 1:
                    builder.Append("3");
                    break;
            }
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/TestMethod3<>%28System.String,System.Int32%29/*/%28\"Value\",3%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithOpenParametersInNonGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTests)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(double));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/TestMethod3<>%28System.String,System.Int32%29/<System.Double>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithParametersInNonGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTests)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(double));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!, ref method, (parameter, ref state, builder) =>
        {
            switch (parameter.Position)
            {
                case 0:
                    builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
                    break;
                case 1:
                    builder.Append("3");
                    break;
            }
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTests/TestMethod3<>%28System.String,System.Int32%29/<System.Double>/%28\"Value\",3%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod1<,>/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod1<,>/<System.Int32,System.Int64>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod1<,>/<System.Int32,System.Int64>/<System.Version,System.Type>"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod1<,>/*/<System.Version,System.Type>"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodInNestedOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>.Nested<>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>%2BNested<>/TestMethod1<,>/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodInNestedClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>.Nested<string>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>%2BNested<>/TestMethod1<,>/<System.Int32,System.Int64,System.String>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodInNestedClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>.Nested<string>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>%2BNested<>/TestMethod1<,>/<System.Int32,System.Int64,System.String>/<System.Version,System.Type>"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodInNestedOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>.Nested<>)
            .GetMethod("TestMethod1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>%2BNested<>/TestMethod1<,>/*/<System.Version,System.Type>"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithOpenParametersInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/*/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithOpenParametersInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/<System.Int32,System.Int64>/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithOpenParametersInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/<System.Int32,System.Int64>/<System.Version,System.Type>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithOpenParametersInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/*/<System.Version,System.Type>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithOpenReferenceParametersInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod3<,>%28%210,%21%210%29/*/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithOpenReferenceParametersInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod3<,>%28%210,%21%210%29/<System.Int32,System.Int64>/*/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithOpenReferenceParametersInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod3<,>%28%210,%21%210%29/<System.Int32,System.Int64>/<System.Version,System.Type>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithOpenReferenceParametersInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod3", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!);

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod3<,>%28%210,%21%210%29/*/<System.Version,System.Type>/*"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithParametersInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!, ref method, (parameter, ref state, builder) =>
        {
            switch (parameter.Position)
            {
                case 0:
                    builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
                    break;
                case 1:
                    builder.Append("3");
                    break;
            }
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/*/*/%28\"Value\",3%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void OpenGenericMethodWithParametersInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!, ref method, (parameter, ref state, builder) =>
        {
            switch (parameter.Position)
            {
                case 0:
                    builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
                    break;
                case 1:
                    builder.Append("3");
                    break;
            }
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/<System.Int32,System.Int64>/*/%28\"Value\",3%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithParametersInClosedGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<int, long>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!, ref method, (parameter, ref state, builder) =>
        {
            switch (parameter.Position)
            {
                case 0:
                    builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
                    break;
                case 1:
                    builder.Append("3");
                    break;
            }
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/<System.Int32,System.Int64>/<System.Version,System.Type>/%28\"Value\",3%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }

    [NUnit.Framework.Test]
    public void ClosedGenericMethodWithParametersInOpenGenericType()
    {
        MethodInfo? method = typeof(TreeNodeFilterHelperTest_GenericClass<,>)
            .GetMethod("TestMethod2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .MakeGenericMethod(typeof(Version), typeof(Type));

        Assert.That(method, Is.Not.Null);

        string name = TreeNodeFilterHelper.GetMethodFilter(method!, ref method, (parameter, ref state, builder) =>
        {
            switch (parameter.Position)
            {
                case 0:
                    builder.Append(TreeNodeFilterHelper.Escape("\"Value\""));
                    break;
                case 1:
                    builder.Append("3");
                    break;
            }
        });

        Console.WriteLine(name);

        Assert.That(name, Is.EqualTo("/Unturned.uTest.Tests/uTest_Test/TreeNodeFilterHelperTest_GenericClass<,>/TestMethod2<,>%28System.String,System.Int32%29/*/<System.Version,System.Type>/%28\"Value\",3%29"));

        Console.WriteLine(HttpUtility.UrlDecode(name));
    }
}

internal class TreeNodeFilterHelperTest_GenericClass<T1, T2>
{
    private void TestMethod1<T3, T4>() { }
    private void TestMethod2<T3, T4>(string str, int i) { }
    private void TestMethod3<T3, T4>(T1 t1, T3 t3) { }
    internal class Nested<T3>
    {
        private void TestMethod1<T4, T5>() { }
    }
}