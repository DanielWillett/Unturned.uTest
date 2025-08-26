using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace uTest.Protocol;

/// <summary>
/// Message sent by the module to inform the runner about a test completion.
/// </summary>
public class ReportTestResultMessage : ITransportMessage
{
    public string SessionUid { get; set; } = null!;
    public string Uid { get; set; } = null!;
    public string LogPath { get; set; } = null!;
    public TestResult Result { get; set; }

    public static ReportTestResultMessage InProgress(string sessionUid, string methodUid)
    {
        return new ReportTestResultMessage
        {
            SessionUid = sessionUid,
            LogPath = string.Empty,
            Result = TestResult.InProgress,
            Uid = methodUid
        };
    }

    /// <inheritdoc />
    public async Task WriteAsync(Stream stream, CancellationToken token)
    {
        ushort sessionIdLen = checked ( (ushort)Encoding.UTF8.GetByteCount(SessionUid) );
        ushort methodIdLen  = checked ( (ushort)Encoding.UTF8.GetByteCount(Uid) );
        ushort logPathLen   = checked ( (ushort)Encoding.UTF8.GetByteCount(LogPath) );

        int length = sessionIdLen + methodIdLen + logPathLen + 7;

        byte[] data = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            data[0] = (byte)TestEnvironmentMessageType.PredefinedMessage;
            data[1] = 4;

            data[2] = (byte)Result;

            Unsafe.WriteUnaligned(ref data[3], sessionIdLen);
            Encoding.UTF8.GetBytes(SessionUid, 0, SessionUid.Length, data, 5);
            
            int index = 5 + sessionIdLen;

            Unsafe.WriteUnaligned(ref data[index], methodIdLen);
            index += 2;
            Encoding.UTF8.GetBytes(Uid, 0, Uid.Length, data, index);
            index += methodIdLen;

            Unsafe.WriteUnaligned(ref data[index], logPathLen);
            index += 2;
            Encoding.UTF8.GetBytes(LogPath, 0, LogPath.Length, data, index);
            index += logPathLen;

            await stream.WriteAsync(data, 0, index, token);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    /// <inheritdoc />
    public int Read(ArraySegment<byte> data)
    {
        if (data.Count < 7)
        {
            throw new FormatException("Malformed ReportTestResultMessage.");
        }

        byte[] arr = data.Array!;
        int offset = data.Offset;
        int maxSize = offset + data.Count;

        Result = (TestResult)arr[offset];
        ++offset;

        ushort length = Unsafe.ReadUnaligned<ushort>(ref arr[offset]);
        offset += 2;
        AssertLength(offset, maxSize, length);
        SessionUid = Encoding.UTF8.GetString(arr, offset, length);
        offset += length;

        AssertLength(offset, maxSize, 2);
        length = Unsafe.ReadUnaligned<ushort>(ref arr[offset]);
        offset += 2;
        AssertLength(offset, maxSize, length);
        Uid = Encoding.UTF8.GetString(arr, offset, length);
        offset += length;

        AssertLength(offset, maxSize, 2);
        length = Unsafe.ReadUnaligned<ushort>(ref arr[offset]);
        offset += 2;
        AssertLength(offset, maxSize, length);
        LogPath = Encoding.UTF8.GetString(arr, offset, length);
        offset += length;

        return offset;

        static void AssertLength(int offset, int maxSize, int needed)
        {
            if (offset + needed > maxSize)
                throw new FormatException("Malformed ReportTestResultMessage.");
        }
    }
}
