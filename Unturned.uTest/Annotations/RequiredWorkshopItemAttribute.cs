using JetBrains.Annotations;
using System;

namespace uTest;

/// <summary>
/// Adds a Workshop mod that must be loaded at runtime.
/// Note that there's no guarantee that other workshop items won't be loaded when tests are ran, just that this item will be loaded.
/// <para>
/// By default the tester will run the test on any map, usually PEI.
/// </para>
/// </summary>
/// <remarks>Maps also need to be specified using the <see cref="RequiredMapAttribute"/>.</remarks>
/// <param name="workshopItemId">
/// The Steam Workshop ID of a mod that's required for all tests within the annotated member.
/// The Workshop ID can be found within the URL of the mod on the Steam Workshop page.
/// </param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public sealed class RequiredWorkshopItemAttribute(ulong workshopItemId) : Attribute
{
    /// <summary>
    /// The Steam Workshop ID of a mod that's required for all tests within the annotated member.
    /// </summary>
    /// <remarks>
    /// The Workshop ID can be found within the URL of the mod on the Steam Workshop page.
    /// </remarks>
    public ulong WorkshopItemId { get; } = workshopItemId;
}