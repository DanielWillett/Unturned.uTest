namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest runner instructing the module to run all tests described in the settings file.
/// </summary>
public class RunTestsMessage : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 3;
}