namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest module letting the runner know that it's OK to revert server-only module changes.
/// </summary>
/// <remarks>Only sent if remote dummies are present.</remarks>
public class ReadyToRevertModuleChanges : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 7;
}