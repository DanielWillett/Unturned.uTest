using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace uTest.Protocol;

public class PingMessage : ITransportMessage
{
    public DateTime TimeStamp { get; private set; }
    public DateTime OriginalTimeStamp { get; private set; }
    public bool IsInitial { get; private set; }

    public PingMessage() { }

    public PingMessage(bool isInitial, DateTime originalTimeStamp = default)
    {
        TimeStamp = DateTime.UtcNow;
        IsInitial = isInitial;
        OriginalTimeStamp = originalTimeStamp;
    }
    public PingMessage(bool isInitial, DateTime timeStamp, DateTime originalTimeStamp)
    {
        TimeStamp = timeStamp;
        IsInitial = isInitial;
        OriginalTimeStamp = originalTimeStamp;
    }

    /// <inheritdoc />
    public async Task WriteAsync(Stream stream, CancellationToken token)
    {
        byte[] data = ArrayPool<byte>.Shared.Rent(19);

        try
        {
            data[0] = (byte)TestEnvironmentMessageType.PredefinedMessage;
            data[1] = 0;
            data[2] = IsInitial ? (byte)1 : (byte)0;

            long ts = TimeStamp.Ticks;
            MemoryMarshal.Write(data.AsSpan(3), ref ts);

            if (OriginalTimeStamp.Kind == DateTimeKind.Utc)
            {
                ts = OriginalTimeStamp.Ticks;
                MemoryMarshal.Write(data.AsSpan(11), ref ts);
            }

            await stream.WriteAsync(data, 0, 19, token);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    /// <inheritdoc />
    public int Read(ArraySegment<byte> data)
    {
        if (data.Count < 17)
            throw new ArgumentOutOfRangeException(nameof(data));

        long ticks = MemoryMarshal.Read<long>(data.AsSpan(1));
        long originalTicks = MemoryMarshal.Read<long>(data.AsSpan(9));

        IsInitial = data.Array![data.Offset] != 0;
        TimeStamp = new DateTime(ticks, DateTimeKind.Utc);
        OriginalTimeStamp = originalTicks == 0 ? default : new DateTime(originalTicks, DateTimeKind.Utc);
        return 17;
    }
}