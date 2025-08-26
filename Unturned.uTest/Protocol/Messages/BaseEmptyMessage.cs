using System;
using System.Buffers;
using System.IO;

namespace uTest.Protocol;

/// <summary>
/// A message with no content (a notification).
/// </summary>
public abstract class BaseEmptyMessage : ITransportMessage
{
    protected abstract byte Id { get; }

    /// <inheritdoc />
    public async Task WriteAsync(Stream stream, CancellationToken token)
    {
        byte[] data = ArrayPool<byte>.Shared.Rent(2);

        try
        {
            data[0] = (byte)TestEnvironmentMessageType.PredefinedMessage;
            data[1] = Id;

            await stream.WriteAsync(data, 0, 2, token);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    /// <inheritdoc />
    public int Read(ArraySegment<byte> data) => 0;
}