namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest module letting the runner know that all dummy clients have started launching, if there are any.
/// </summary>
/// <remarks>Always sent, even if there are no dummies.</remarks>
public class AllInstancesStartedMessage : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 6;
}