using Microsoft.Testing.Platform.Extensions.Messages;
using uTest.Discovery;

namespace uTest.Runner;

#pragma warning disable TPEXP

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

        TrxSwitch.AddTrxTestProperties(in instance, bag);
    }

    public static void AddPropertiesFromSummary(in this UnturnedTestInstance instance, TestExecutionSummary summary, PropertyBag bag)
    {
        TrxSwitch.AddTrxTestSummaryProperties(summary, bag);

        if (summary.TimingSteps != null || summary.StartTime > DateTimeOffset.MinValue)
        {
            TimingInfo globalInfo = new TimingInfo(summary.StartTime, summary.EndTime, summary.Duration);
            TimingProperty property;
            if (summary.TimingSteps is { Count: > 0 })
            {
                StepTimingInfo[] steps = new StepTimingInfo[summary.TimingSteps.Count];
                for (int i = 0; i < steps.Length; ++i)
                {
                    TestTimingStep step = summary.TimingSteps[i];
                    steps[i] = new StepTimingInfo(
                        step.Id,
                        step.Description ?? step.Id,
                        new TimingInfo(step.StartTime, step.EndTime, step.Duration)
                    );
                }

                property = new TimingProperty(globalInfo, steps);
            }
            else
            {
                property = new TimingProperty(globalInfo);
            }

            bag.Add(property);
        }

        if (summary.Artifacts is { Count: > 0 })
        {
            foreach (TestArtifact artifact in summary.Artifacts)
            {
                FileInfo file;
                try
                {
                    file = new FileInfo(artifact.FileName);
                    if (!file.Exists)
                        continue;
                }
                catch (SystemException)
                {
                    continue;
                }

                bag.Add(new FileArtifactProperty(file, artifact.DisplayName, artifact.Description));
            }
        }

        if (!string.IsNullOrEmpty(summary.StandardError))
        {
            bag.Add(new StandardErrorProperty(summary.StandardError!));
        }

        if (!string.IsNullOrEmpty(summary.StandardOutput))
        {
            bag.Add(new StandardOutputProperty(summary.StandardOutput!));
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
