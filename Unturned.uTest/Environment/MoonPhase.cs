namespace uTest.Environment;

/// <summary>
/// Unturned moon phase.
/// </summary>
public enum MoonPhase
{
    /// <summary>
    /// Small portion of moon is showing, but the visible section is getting larger.
    /// </summary>
    WaxingCrescent,

    /// <summary>
    /// Large portion of moon is showing and the visible section is getting larger.
    /// </summary>
    WaxingGibbous,

    /// <summary>
    /// Entire moon is visible, has in-game behavior changes.
    /// </summary>
    Full,

    /// <summary>
    /// Large portion of moon is showing, but the visible section is getting smaller.
    /// </summary>
    WaningGibbous,

    /// <summary>
    /// Small portion of moon is showing and the visible section is getting smaller.
    /// </summary>
    WaningCresent
}