using System.Reflection;
using uTest.Discovery;

namespace uTest.Runner;

internal class GeneratedTestRegistrationListCapability : GeneratedTestRegistrationList, ITestRegistrationListCapability
{
    public GeneratedTestRegistrationListCapability(Assembly assembly) : base(assembly) { }
}
