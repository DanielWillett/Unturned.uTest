using System;
using System.IO;

namespace uTest.Protocol;

public interface ITransportMessage
{
    /// <summary>
    /// Writes this message to a stream. Writing is responsible for writing the message ID or type name.
    /// </summary>
    Task WriteAsync(Stream stream, CancellationToken token);

    /// <summary>
    /// Reads this message from a byte array. This data does not include the message ID or type name.
    /// </summary>
    /// <returns>The exact number of bytes read.</returns>
    int Read(ArraySegment<byte> data);
}