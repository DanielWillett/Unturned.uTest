using System;
using System.Reflection;

namespace uTest.Compat.Tests;

/// <summary>
/// An instance of a unit test.
/// </summary>
public interface IUnitTestExecution : IEquatable<IUnitTestExecution>
{
    /// <summary>
    /// The unique ID of this test session.
    /// </summary>
    string SessionUid { get; }
    
    /// <summary>
    /// The unique ID of this test method.
    /// </summary>
    string Uid { get; }

    /// <summary>
    /// The test method being invoked.
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// The display name of the test.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// A string following the <see href="https://github.com/microsoft/testfx/blob/main/docs/mstest-runner-graphqueryfiltering/graph-query-filtering.md">Graph Query Filter</see> pattern:
    /// <c>/Assembly/Namespace/Type/Method[/other details]</c> to uniquely identify a test method.
    /// </summary>
    /// <remarks>Used with the '--treenode-filter' command line option for the MTP runner.</remarks>
    string TreePath { get; }

    /// <summary>
    /// A string following the <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md#managedtype-property">ManagedType</see> format for VSTest.
    /// </summary>
    string ManagedType { get; }

    /// <summary>
    /// A string following the <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md#managedmethod-property">ManagedMethod</see> format for VSTest.
    /// </summary>
    string ManagedMethod { get; }

    /// <summary>
    /// The index of this test within it's set of parameter variations. Does not include type parameters.
    /// </summary>
    int VariationIndex { get; }

    /// <summary>
    /// The number of serverside players ('dummies') allocated to this test.
    /// </summary>
    int AllocatedServersidePlayers { get; }

    /// <summary>
    /// Gets the type parameters used for the test.
    /// </summary>
    /// <param name="containingTypeArgs">The type parameters given to the containing type.</param>
    /// <param name="methodTypeArgs">The type parameters given to the method.</param>
    void GetTypeParameters(out IReadOnlyList<Type> containingTypeArgs, out IReadOnlyList<Type> methodTypeArgs);

    /// <summary>
    /// Gets the method parameter values used for the test.
    /// </summary>
    IReadOnlyList<object?> GetParameters();

    /// <summary>
    /// Gets a list of the Steam64 IDs of all allocated serverside players ('dummies').
    /// </summary>
    IReadOnlyList<CSteamID> GetAllocatedServersidePlayerIds();
}