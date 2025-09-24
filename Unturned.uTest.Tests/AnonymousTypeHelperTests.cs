using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using uTest;
using Assert = NUnit.Framework.Assert;
using TestAttribute = NUnit.Framework.TestAttribute;

namespace uTest_Test;

public class AnonymousTypeHelperTests
{
    public static int V_1 = 3;

    [Test]
    public void TestReadFromSingleValueNoConvert()
    {
        FieldInfo field = GetField(() => V_1);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_1, GetMethod(TestMethod), field, out object[] args));

        object[] expected = [ V_1 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(int num) { }
    }

    [Test]
    public void TestReadFromSingleValueWithConvert()
    {
        FieldInfo field = GetField(() => V_1);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_1, GetMethod(TestMethod), field, out object[] args));

        object[] expected = [ (long)V_1 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(long num) { }
    }

    public static ValueTuple<int> V_2 = new ValueTuple<int>(3);

    [Test]
    public void TestReadFrom1TupleNoConvert()
    {
        FieldInfo field = GetField(() => V_2);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_2, GetMethod(TestMethod), field, out object[] args));

        object[] expected = [ V_2.Item1 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(int num) { }
    }

    [Test]
    public void TestReadFrom1TupleWithConvert()
    {
        FieldInfo field = GetField(() => V_2);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_2, GetMethod(TestMethod), field, out object[] args));

        object[] expected = [ (long)V_2.Item1 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(long num) { }
    }

    public static (int num2, long num1) V_3 = (3, 18);

    [Test]
    public void TestReadFromTupleByNameNoConvert()
    {
        FieldInfo field = GetField(() => V_3);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_3, GetMethod(TestMethod), field, out object[] args));

        object[] expected = [ V_3.num1, V_3.Item1 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(long num1, int num2) { }
    }

    [Test]
    public void TestReadFromTupleByNameWithConvert()
    {
        FieldInfo field = GetField(() => V_3);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_3, GetMethod(TestMethod), field, out object[] args));
        
        object[] expected = [ (ulong)V_3.num1, (byte)V_3.Item1 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(ulong num1, byte num2) { }
    }

    public static object V_4 = new { num2 = 3, num1 = 18L, extra = "" };

    [Test]
    public void TestReadFromAnonymousTypeNoConvert()
    {
        FieldInfo field = GetField(() => V_4);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_4, GetMethod(TestMethod), field, out object[] args));

        object[] expected = [ 18L, 3 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(long num1, int num2) { }
    }

    [Test]
    public void TestReadFromAnonymousTypeWithConvert()
    {
        FieldInfo field = GetField(() => V_4);

        Assert.That(AnonymousTypeHelper.TryMapObjectToMethodParameters(V_4, GetMethod(TestMethod), field, out object[] args));
        
        object[] expected = [ 18UL, (byte)3 ];
        Assert.That(args, Is.EquivalentTo(expected));
        return;

        static void TestMethod(ulong num1, byte num2) { }
    }

    [DebuggerStepThrough]
    private static FieldInfo GetField<T>(Expression<Func<T>> func)
    {
        return (FieldInfo)((MemberExpression)func.Body).Member;
    }

    [DebuggerStepThrough]
    private static MethodInfo GetMethod(Delegate d)
    {
        return d.Method;
    }
}
