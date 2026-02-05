namespace uTest.Protocol;

/// <summary>
/// Sent by the runner to let the module know that all server-only module changes have been reverted, so that it's OK to start launching dummies.
/// </summary>
public class ServerModuleChangesReverted : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 8;
}