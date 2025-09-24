using Microsoft.Testing.Platform.Extensions.Messages;
using uTest.Discovery;

namespace uTest.Runner;

internal static class TestExtensions
{
    public static void AddProperties(in this UnturnedTestInstance instance, PropertyBag bag)
    {
        if (instance.Test is UnturnedMTPTest mtpTest)
        {
            if (mtpTest.LocationInfo != null)
                bag.Add(mtpTest.LocationInfo);
            if (mtpTest.IdentifierInfo != null)
                bag.Add(mtpTest.IdentifierInfo);
        }
    }

    public static TestNode CreateTestNode(in this UnturnedTestInstance instance, out TestNodeUid? parentUid)
    {
        TestNode node = new TestNode
        {
            DisplayName = instance.DisplayName,
            Uid = new TestNodeUid(instance.Uid)
        };

        instance.AddProperties(node.Properties);

        parentUid = null;
        if (instance.HasParameters)
        {
            parentUid = new TestNodeUid(instance.Test.Uid);
        }

        return node;
    }

    public static TestNode CreateTestNode(in this UnturnedTestInstance instance)
    {
        return instance.CreateTestNode(out _);
    }

}
