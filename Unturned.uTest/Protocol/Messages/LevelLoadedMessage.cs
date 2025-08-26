namespace uTest.Protocol;

/// <summary>
/// Sent by the uTest module when the level loads.
/// </summary>
public class LevelLoadedMessage : BaseEmptyMessage
{
    /// <inheritdoc />
    protected override byte Id => 1;
}