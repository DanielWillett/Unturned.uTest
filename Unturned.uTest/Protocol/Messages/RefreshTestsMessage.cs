namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest runner when the test file has been updated.
/// </summary>
public class RefreshTestsMessage : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 2;
}