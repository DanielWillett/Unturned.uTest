using uTest.Discovery;

namespace uTest.Module;

internal class UnturnedTestInstanceData
{
    public readonly UnturnedTestInstance Instance;
    public PlayerSimulationMode SimulationMode;
    public int Dummies;
    internal IServersideTestPlayer[]? AllocatedDummies;

    public UnturnedTestInstanceData(UnturnedTestInstance instance)
    {
        Instance = instance;
    }
}
