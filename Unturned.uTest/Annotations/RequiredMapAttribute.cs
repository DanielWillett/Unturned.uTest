using JetBrains.Annotations;
using System;

namespace uTest;

/// <summary>
/// The name of the map to use for all tests within the annotated member.
/// <para>
/// By default the tester will run the test on any map, usually PEI unless another test required a map.
/// </para>
/// </summary>
/// <param name="mapName">
/// The name of the map to use for all tests within the annotated member.
/// If it's a workshop map it should also be added as a workshop dependency using the <see cref="RequiredWorkshopItemAttribute"/>.
/// </param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module)]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public sealed class RequiredMapAttribute(string? mapName) : Attribute
{
    /// <summary>
    /// The name of the map to use for all tests within the annotated member.
    /// If it's a workshop map it should also be added as a workshop dependency using the <see cref="RequiredWorkshopItemAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The value of this property is the same as the map name that would be put in the <c>Commands.dat</c> file.
    /// It's the case-sensitive name of the folder containg the map.
    /// </remarks>
    public string? MapName { get; } = mapName;
}