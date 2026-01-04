using uTest.Discovery;

namespace uTest.Module;

internal class UnturnedTestInstanceData
{
    public readonly UnturnedTestInstance Instance;
    public PlayerSimulationMode SimulationMode;
    public int Dummies;

    public UnturnedTestInstanceData(UnturnedTestInstance instance)
    {
        Instance = instance;
    }
}
