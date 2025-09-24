using NUnit.Framework;
using System;
using System.Collections.Generic;
using uTest.Runner;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

namespace uTest_Test;
internal class TestDisplayNameFormatterTests
{
    [Test]
    [TestCase(typeof(decimal), "decimal")]
    [TestCase(typeof(object), "object")]
    [TestCase(typeof(string), "string")]
    [TestCase(typeof(int[]), "int[]")]
    [TestCase(typeof(int[][,]), "int[][,]")]
    [TestCase(typeof(int[][,]*[,,,][]*), "int[][,]*[,,,][]*")]
    [TestCase(typeof(Version), "System.Version")]
    [TestCase(typeof(KeyValuePair<string, int[,][]>), "System.Collections.Generic.KeyValuePair<string, int[,][]>")]
    public void TypeAsExpected(Type type, string expected)
    {
        string name = TestDisplayNameFormatter.GetTypeDisplayName(type);

        Assert.That(name, Is.EqualTo(expected));

        Console.WriteLine(name);
    }
}
