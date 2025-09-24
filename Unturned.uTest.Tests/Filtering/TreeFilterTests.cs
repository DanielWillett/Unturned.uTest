using Microsoft.Testing.Platform.Requests;
using NUnit.Framework;
using uTest;
using uTest.Runner.Util;
using Assert = NUnit.Framework.Assert;

namespace uTest_Test.Filtering;

#pragma warning disable TPEXP

public class TreeFilterTests
{
    [NUnit.Framework.Test]
    [TestCase("/*/uTest_Test.Filtering/*")]
    [TestCase("/*/*/TreeFilterTests")]
    [TestCase("/*/*/**")]
    [TestCase("/*/uTest_Test.Filtering/TreeFilterTests/*")]
    [TestCase("/*/uTest_Test.Filtering/TreeFilterTests")]
    [TestCase("/*/uTest_Test.Filtering/TreeFilterTests[Category=3]/**")]
    [TestCase("/*/uTest_Test.Filtering/*[Category=3]")]
    [TestCase("/*/uTest_Test.Filtering/**")]
    [TestCase("/*/**")]
    [TestCase("/*/uTest_Test.Filtering/TreeFilterTests/TestBasicTree")]
    [TestCase("/*/uTest_Test.Filtering/TreeFilterTests/TestBasicTree/<System.Int32,System.String>/<System.String>/%28\"String\"%29")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/*")]
    [TestCase("/Unturned.uTest.Tests/*/TreeFilterTests")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/TreeFilterTests/*")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/TreeFilterTests")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/TreeFilterTests[Category=3]/**")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/*[Category=3]")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/**")]
    [TestCase("/Unturned.uTest.Tests/**")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/TreeFilterTests/TestBasicTree")]
    [TestCase("/Unturned.uTest.Tests/uTest_Test.Filtering/TreeFilterTests/TestBasicTree/<System.Int32,System.String>/<System.String>/%28\"String\"%29")]
    public void TestBasicTree(string shouldMatch)
    {
        TreeNodeFilter filter = MTPFilterHelper.CreateTreeFilter(shouldMatch);

        Assert.That(FilterHelper.PotentiallyMatchesFilter(typeof(TreeFilterTests), new TreeNodeFilterWrapper(filter)));
    }

    [NUnit.Framework.Test]
    [TestCase("/Unturned.uTest/uTestx_Test.Filtering/TreeFilterTests/TestBasicTree")]
    [TestCase("/Unturned.uTest/uTest_Test.Filtering/TreexFilterTests/TestBasicTree")]
    [TestCase("/Unturned.uTest/123/456/789")]
    [TestCase("/Unturned.uTest/uTest_Test.Filtering/TxreeFilterTests/TestBasicTree/<System.Int32,System.String>/<System.String>/%28\"String\"%29")]
    [TestCase("/mscorlib/uTestx_Test.Filtering/TreeFilterTests/TestBasicTree")]
    [TestCase("/mscorlib/uTest_Test.Filtering/TreexFilterTests/TestBasicTree")]
    [TestCase("/mscorlib/123/456/789")]
    [TestCase("/mscorlib/uTest_Test.Filtering/TxreeFilterTests/TestBasicTree/<System.Int32,System.String>/<System.String>/%28\"String\"%29")]
    public void TestBasicTreeShortCircuits(string shouldntMatch)
    {
        TreeNodeFilter filter = MTPFilterHelper.CreateTreeFilter(shouldntMatch);

        Assert.That(FilterHelper.PotentiallyMatchesFilter(typeof(TreeFilterTests), new TreeNodeFilterWrapper(filter)), Is.False);
    }
}