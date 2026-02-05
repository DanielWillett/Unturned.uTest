using System;
using System.Diagnostics;
using System.Reflection;
using uTest.Compat.Tests;
using uTest.Discovery;

namespace uTest.Module;

[DebuggerDisplay("{DisplayName,nq}")]
internal class UnturnedTestInstanceData : IUnitTestExecution
{
    public readonly UnturnedTestInstance Instance;
    public PlayerSimulationMode SimulationMode;
    public int Dummies;
    internal IServersideTestPlayer[]? AllocatedDummies;

    public string Uid => Instance.Uid;
    public MethodInfo Method => Instance.Method;
    public string DisplayName => Instance.DisplayName;
    public string TreePath => Instance.TreePath;
    public string ManagedType => Instance.ManagedType;
    public string ManagedMethod => Instance.ManagedMethod;
    public int VariationIndex => Instance.Index;
    public int AllocatedServersidePlayers => Dummies;

    public string SessionUid { get; }

    public UnturnedTestInstanceData(UnturnedTestInstance instance, string sessionUid)
    {
        SessionUid = sessionUid;
        Instance = instance;
    }

    public void GetTypeParameters(out IReadOnlyList<Type> typeArgs, out IReadOnlyList<Type> methodArgs)
    {
        typeArgs = Instance.TypeArgs;
        methodArgs = Instance.MethodTypeArgs;
    }
    
    public IReadOnlyList<object?> GetParameters()
    {
        return Instance.Arguments;
    }

    public IReadOnlyList<CSteamID> GetAllocatedServersidePlayerIds()
    {
        IServersideTestPlayer[]? dummies = AllocatedDummies;
        if (dummies == null || dummies.Length == 0)
        {
            return Array.Empty<CSteamID>();
        }

        CSteamID[] arr = new CSteamID[dummies.Length];
        for (int i = 0; i < dummies.Length; ++i)
            arr[i] = dummies[i].Steam64;

        return arr;
    }

    public bool Equals(IUnitTestExecution other)
    {
        return other == this;
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
