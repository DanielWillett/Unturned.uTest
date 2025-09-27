Work in progress...

# uTest for Unturned
uTest is a Unit Testing solution for Unturned plugins and modules, client or server-side.

When you choose to run your tests, an instance of Unturned opens, executes the tests, then closes as quick as possible, reporting results to the IDE.

### Assertions (xunit)
uTest uses [**xunit** assertions](https://www.nuget.org/packages/xunit.assert) as a standalone package, so you don't have to learn yet another unit testing library's assertion syntax.

### Source Generation
uTest uses source generation to statically discover tests so what you see in your IDE is what is ran.

### Microsoft Testing Platform
uTest is built on the [Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro) library which provides a modern unit test API that can run on basically any device and supports most modern IDEs. MTP is still in the experimental phase so some issues could occur.

### .NET Standard or Framework
uTest supports projects targeting either **.NET Framework 4.7.1+** or **.NET Standard 2.1**. It also works with multi-targeting.

### Integration
uTest integrates into any IDE's that support MTP, including Visual Studio, VS-Code, and Rider. MTP generates an executable file so you can also just run the tests using the `dotnet .\test\file.dll` command. It does not work with `dotnet test` just yet due to .NET Standard libraries not being executable.

### TRX Reporting
uTest supports TRX report generation via the `--report-trx` command line argument. You need to reference the MTP TRX extension as well.
```xml
<PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="*" />
```

## Examples

```cs
// all tests must implement ITestClass or one of its child interfaces
[Test]
public class PlayerTests : ITestClassSetup, ITestClassTearDown
{
    private IDisposable _something;

    // special callbacks use the ITestClassXxxx interfaces
    public async ValueTask SetupAsync(ITestContext testContext, CancellationToken token)
    {
        await testContext.ConfigureAsync(
            b => b.WithPlayers(4)
                  .WithTime(12, 0, 0, timeProgression: false)
        );

        _something = /* whatever */;
    }

    public async ValueTask TearDownAsync(CancellationToken token)
    {
        await _something.DisposeAsync();
    }

    [Test]
    public void ServerHasPlayers()
    {
        Assert.NotEmpty(Provider.clients);
    }
    
    // parameters can be filled using [TestArgs] attributes
    [Test]
    [TestArgs(1, 2, 3)]
    [TestArgs(4, -2, 2)]
    public void CorrectSum(int l, int r, int sum)
    {
        Assert.Equals(sum, l + r);
    }
    
    // they can also use the [Set] and [Range] attributes
    [Test]
    public void StressTest([Set(1, 5, 10)] int playerCount, [Set(true, false)] bool isUiReworkDone)
    {
        // this test will run 3*2 = 6 times
    }
    
    // generic type parameters can use [Set] and [TypeArgs] attributes.
    // they can also go on the test class
    [Test]
    public async Task ParseDecimal<[Set(typeof(decimal), typeof(double), typeof(float))] T>(
        
        [Range(-10.0, 10.0, step: 0.1)]
        string decimalStr
    )
    {
        // ...
    }
}
```

## Installation

Put the following properties and references in your csproj file:
```xml
<PropertyGroup>

    <IsUnturnedTestProject>True</IsUnturnedTestProject>

</PropertyGroup>

<ItemGroup>

    <PackageReference Include="Unturned.uTest" Version="*" />
    <PackageReference Include="Unturned.uTest.Runner" Version="*" />

</ItemGroup>
```

### Visual Studio
No changes are required.

### Visual Studio Code (C# Extension) Installation
Enable the **Use Testing Platform Protocol** setting: `dotnet.testWindow.useTestingPlatformProtocol`.

### Rider
Follow instructions [here](https://www.jetbrains.com/help/rider/Unit_Testing__Index.html) for TUnit (it also uses MTP).

### Others
Run `dotnet ./TestProject/bin/Debug/netstandard2.1/TestProject.dll --treenode-filter [see below]`. The path to the dll may vary.


## Filtering

### Tree-Node
uTest supports MTP tree-node filters to select tests easier with the `--treenode-filter` command line argument. The basic format is as follows:

`/Assembly/Namespace/Type/Method`

If the method has parameters it may look more like this:
`/Assembly/Namespace/Type/Method%28System.String%29`

But usually you can just worry about the name:

#### All tests in *Project.Tests.PlayerTests*
`/TestProject/Project.Tests/PlayerTests/**`

#### All tests named ServerHasPlayers in *Project.Tests.PlayerTests*
`/TestProject/Project.Tests/PlayerTests/ServerHasPlayers*/**`

The full format also includes generic type parameters and parameter values. More examples are [here](https://github.com/DanielWillett/Unturned.uTest/blob/master/Unturned.uTest/Util/TreeNodeFilterHelper.cs#L47).


### UID List
uTest also supports the MTP UID list argument with the `--filter-uid` command line argument.

The format is a bit complex to allow for globally unique stable IDs, but it is described [here](https://github.com/DanielWillett/Unturned.uTest/blob/master/Unturned.uTest/Tests/UnturnedTestUid.cs#L18).