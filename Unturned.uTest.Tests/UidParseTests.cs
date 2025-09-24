using System;
using NUnit.Framework;
using uTest;
using Assert = NUnit.Framework.Assert;

namespace uTest_Test;
public class UidParseTests
{
    [NUnit.Framework.Test]
    [TestCase("4:6:Test.Method", "Test", "Method")]
    [TestCase("11:6:System.Test.Method", "System.Test", "Method")]
    [TestCase("0:2:.''", "", "''")]
    public void CheckBasic(string uid, string type, string method)
    {
        Assert.That(
            UnturnedTestUid.TryParse(uid,
                out ReadOnlyMemory<char> managedType,
                out ReadOnlyMemory<char> managedMethod,
                out ReadOnlyMemory<char>[] managedTypes,
                out object?[] parameters,
                out int variantIndex),
            Is.True
        );

        Assert.That(managedTypes, Is.Empty);
        Assert.That(parameters, Is.Empty);
        Assert.That(variantIndex, Is.Zero);

        Assert.That(managedType.ToString(), Is.EqualTo(type));
        Assert.That(managedMethod.ToString(), Is.EqualTo(method));
    }

    [NUnit.Framework.Test]
    [TestCase("12:13:System.Array.Sort`1(!!0[]) <13:System.String> 2", "System.Array", "Sort`1(!!0[])", new string[] { "System.String" }, 2)]
    [TestCase("12:18:System.Array.Sort(System.Array) 4", "System.Array", "Sort(System.Array)", new string[0], 4)]
    public void CheckVariantIndex(string uid, string type, string method, string[] types, int expectedVariantIndex)
    {
        Assert.That(
            UnturnedTestUid.TryParse(uid,
                out ReadOnlyMemory<char> managedType,
                out ReadOnlyMemory<char> managedMethod,
                out ReadOnlyMemory<char>[] managedTypes,
                out object?[] p,
                out int variantIndex),
            Is.True
        );

        Assert.That(p, Is.Empty);

        Assert.That(managedType.ToString(), Is.EqualTo(type));
        Assert.That(managedMethod.ToString(), Is.EqualTo(method));

        Assert.That(managedTypes.Length, Is.EqualTo(types.Length));

        for (int i = 0; i < managedTypes.Length; ++i)
            Assert.That(managedTypes[i].ToString(), Is.EqualTo(types[i]));

        Assert.That(variantIndex, Is.EqualTo(expectedVariantIndex));
    }

    [NUnit.Framework.Test]
    [TestCase(@"4:21:Test.Method(System.DBNull) (null) 1", "Test", "Method(System.DBNull)", new string[0], new object?[] { null }, 1)]
    [TestCase(@"4:22:Test.Method(System.Decimal) (123.456) 2", "Test", "Method(System.Decimal)", new string[0], new object[] { 123.456 }, 2)]
    [TestCase(@"4:22:Test.Method(System.Decimal) (-123.456) 3", "Test", "Method(System.Decimal)", new string[0], new object[] { -123.456 }, 3)]
    [TestCase(@"4:21:Test.Method(System.Double) (123.456) 4", "Test", "Method(System.Double)", new string[0], new object[] { 123.456d }, 4)]
    [TestCase(@"4:21:Test.Method(System.Double) (-123.456) 5", "Test", "Method(System.Double)", new string[0], new object[] { -123.456d }, 5)]
    [TestCase(@"4:21:Test.Method(System.Single) (123.456) 6", "Test", "Method(System.Single)", new string[0], new object[] { 123.456f }, 6)]
    [TestCase(@"4:21:Test.Method(System.Single) (-123.456) 7", "Test", "Method(System.Single)", new string[0], new object[] { -123.456f }, 7)]
    [TestCase(@"4:21:Test.Method(System.UInt64) (123) 8", "Test", "Method(System.UInt64)", new string[0], new object[] { (ulong)123 }, 8)]
    [TestCase(@"4:21:Test.Method(System.UInt32) (123) 9", "Test", "Method(System.UInt32)", new string[0], new object[] { 123u }, 9)]
    [TestCase(@"4:21:Test.Method(System.UInt16) (123) 10", "Test", "Method(System.UInt16)", new string[0], new object[] { (ushort)123 }, 10)]
    [TestCase(@"4:19:Test.Method(System.Byte) (123) 11", "Test", "Method(System.Byte)", new string[0], new object[] { (byte)123 }, 11)]
    [TestCase(@"4:20:Test.Method(System.Int64) (123) 12", "Test", "Method(System.Int64)", new string[0], new object[] { (long)123 }, 12)]
    [TestCase(@"4:20:Test.Method(System.Int32) (123) 13", "Test", "Method(System.Int32)", new string[0], new object[] { 123 }, 13)]
    [TestCase(@"4:20:Test.Method(System.Int16) (123) 14", "Test", "Method(System.Int16)", new string[0], new object[] { (short)123 }, 14)]
    [TestCase(@"4:20:Test.Method(System.SByte) (123) 15", "Test", "Method(System.SByte)", new string[0], new object[] { (sbyte)123 }, 15)]
    [TestCase(@"4:20:Test.Method(System.Int64) (-123) 16", "Test", "Method(System.Int64)", new string[0], new object[] { (long)-123 }, 16)]
    [TestCase(@"4:20:Test.Method(System.Int32) (-123) 17", "Test", "Method(System.Int32)", new string[0], new object[] { -123 }, 17)]
    [TestCase(@"4:20:Test.Method(System.Int16) (-123) 18", "Test", "Method(System.Int16)", new string[0], new object[] { (short)-123 }, 18)]
    [TestCase(@"4:20:Test.Method(System.SByte) (-123) 19", "Test", "Method(System.SByte)", new string[0], new object[] { (sbyte)-123 }, 19)]
    [TestCase(@"4:19:Test.Method(System.Char) (32) 20", "Test", "Method(System.Char)", new string[0], new object[] { ' ' }, 20)]
    [TestCase(@"4:21:Test.Method(System.String) ("""") 21", "Test", "Method(System.String)", new string[0], new object[] { "" }, 21)]
    [TestCase(@"4:21:Test.Method(System.String) (""a"") 22", "Test", "Method(System.String)", new string[0], new object[] { "a" }, 22)]
    [TestCase(@"4:21:Test.Method(System.String) (null) 23", "Test", "Method(System.String)", new string[0], new object?[] { null }, 23)]
    [TestCase(@"4:34:Test.Method(System.Int32,System.String) (123,""\\ \n\\test\\"") 24", "Test", "Method(System.Int32,System.String)", new string[0], new object[] { 123, "\\ \n\\test\\" }, 24)]
    [TestCase(@"4:20:Test.Method(System.Int32) (123) 25", "Test", "Method(System.Int32)", new string[0], new object[] { 123 }, 25)]
    [TestCase(@"4:39:Test.Method(System.Nullable`1<System.Int32>) (123) 26", "Test", "Method(System.Nullable`1<System.Int32>)", new string[0], new object[] { 123 }, 26)]
    [TestCase(@"4:39:Test.Method(System.Nullable`1<System.Int32>) (null) 27", "Test", "Method(System.Nullable`1<System.Int32>)", new string[0], new object?[] { null }, 27)]
    [TestCase(@"4:53:Test.Method(System.Nullable`1<System.Int32>,System.String) (null,""32"") 28", "Test", "Method(System.Nullable`1<System.Int32>,System.String)", new string[0], new object?[] { null, "32" }, 28)]
    [TestCase(@"4:53:Test.Method(System.Nullable`1<System.Int32>,System.String) (123,""32"") 29", "Test", "Method(System.Nullable`1<System.Int32>,System.String)", new string[0], new object[] { 123, "32" }, 29)]
    [TestCase(@"4:53:Test.Method(System.Nullable`1<System.Int32>,System.String) (123,null) 30", "Test", "Method(System.Nullable`1<System.Int32>,System.String)", new string[0], new object?[] { 123, null }, 30)]
    [TestCase(@"11:6:System.Test.Method <12:System.Int32,13:System.String>", "System.Test", "Method", new string[] { "System.Int32", "System.String" }, new object[0], 0)]
    [TestCase(@"0:17:.''(System.String) <12:System.Int32> (""string"") 32", "", "''(System.String)", new string[] { "System.Int32" }, new object[] { "string" }, 32)]
    public void CheckVariantParameters(string uid, string type, string method, string[] types, object[] parameters, int expectedVariantIndex)
    {
        Assert.That(
            UnturnedTestUid.TryParse(uid,
                out ReadOnlyMemory<char> managedType,
                out ReadOnlyMemory<char> managedMethod,
                out ReadOnlyMemory<char>[] managedTypes,
                out object?[] p,
                out int variantIndex),
            Is.True
        );

        Assert.That(variantIndex, Is.EqualTo(expectedVariantIndex));

        Assert.That(managedType.ToString(), Is.EqualTo(type));
        Assert.That(managedMethod.ToString(), Is.EqualTo(method));

        Assert.That(managedTypes.Length, Is.EqualTo(types.Length));
        Assert.That(p.Length, Is.EqualTo(parameters.Length));

        for (int i = 0; i < managedTypes.Length; ++i)
            Assert.That(managedTypes[i].ToString(), Is.EqualTo(types[i]));

        for (int i = 0; i < p.Length; ++i)
        {
            if (p[i] is decimal d)
                p[i] = (double)d;
            if (p[i] is DBNull && parameters[i] == null)
                parameters[i] = DBNull.Value;
            Assert.That(p[i], Is.EqualTo(parameters[i]));
        }
    }


    [NUnit.Framework.Test]
    [TestCase("Test")]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Test.Method")]
    [TestCase("3:6:Test.Method")]
    [TestCase("4:5:Test.Method")]
    [TestCase("4:7:Test.Method")]
    [TestCase("5:6:Test.Method")]
    [TestCase("5:6:Test")]
    [TestCase("5:6:")]
    [TestCase("0:17:.''(System.String,System.String) (\"string\",\"string\",\"string\")")]
    [TestCase("0:17:.''(System.String,System.String) (\"string\")")]
    [TestCase("0:17:.''(System.Int32) (\"string\")")]
    [TestCase("0:17:.''(System.String) (\"str\\ing\")")] // invalid escape sequence
    [TestCase("12:13:System.Array.Sort(System.Array) -1")]
    public void CheckFail(string uid)
    {
        Assert.That(UnturnedTestUid.TryParse(uid, out _, out _, out _, out _, out _), Is.False);
    }
}
