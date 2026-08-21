namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest runner instructing the module to shut down.
/// </summary>
internal sealed class GracefulShutdownMessage : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 5;
}