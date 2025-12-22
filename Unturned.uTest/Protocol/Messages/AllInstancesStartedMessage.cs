namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest module letting the runner know that it's OK to disable the module.
/// </summary>
public class AllInstancesStartedMessage : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 6;
}