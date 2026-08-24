using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lib.GAB.Protocol;
using Lib.GAB.Transport;
using Xunit;

namespace Lib.GAB.Tests;

public class TcpConnectionWriteTests
{
    [Fact]
    public async Task ConcurrentSendsRemainCompleteSerializedFrames()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var acceptTask = listener.AcceptTcpClientAsync();
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverClient = await acceptTask;
        using var stream = new YieldingRecordingStream();
        using var connection = new TcpConnection(serverClient, stream);

        const int messageCount = 32;
        var sends = Enumerable.Range(0, messageCount)
            .Select(index => connection.SendMessageAsync(new GabpResponse
            {
                Id = $"response-{index}",
                Result = new
                {
                    index,
                    payload = new string((char)('a' + index % 26), 256)
                }
            }))
            .ToArray();

        await Task.WhenAll(sends);

        Assert.False(stream.OverlapDetected);
        var ids = ReadFrameIds(stream.ToArray());
        Assert.Equal(messageCount, ids.Count);
        Assert.Equal(messageCount, ids.Distinct(StringComparer.Ordinal).Count());
        for (var index = 0; index < messageCount; index++)
        {
            Assert.Contains($"response-{index}", ids);
        }
    }

    private static IReadOnlyList<string> ReadFrameIds(byte[] bytes)
    {
        var ids = new List<string>();
        var offset = 0;
        var headerTerminator = Encoding.ASCII.GetBytes("\r\n\r\n");

        while (offset < bytes.Length)
        {
            var headerEnd = IndexOf(bytes, headerTerminator, offset);
            Assert.True(headerEnd >= offset, "Frame header terminator was not found.");

            var header = Encoding.ASCII.GetString(bytes, offset, headerEnd - offset);
            var contentLengthLine = header
                .Split(new[] { "\r\n" }, StringSplitOptions.None)
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var contentLength = int.Parse(contentLengthLine.Substring("Content-Length:".Length).Trim());
            var bodyStart = headerEnd + headerTerminator.Length;
            Assert.True(bodyStart + contentLength <= bytes.Length, "Frame body is incomplete.");

            using var document = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, bodyStart, contentLength));
            ids.Add(document.RootElement.GetProperty("id").GetString() ?? string.Empty);
            offset = bodyStart + contentLength;
        }

        return ids;
    }

    private static int IndexOf(byte[] bytes, byte[] value, int start)
    {
        for (var index = start; index <= bytes.Length - value.Length; index++)
        {
            var matched = true;
            for (var valueIndex = 0; valueIndex < value.Length; valueIndex++)
            {
                if (bytes[index + valueIndex] == value[valueIndex])
                    continue;

                matched = false;
                break;
            }

            if (matched)
                return index;
        }

        return -1;
    }

    private sealed class YieldingRecordingStream : MemoryStream
    {
        private int _activeWrites;
        private int _overlapDetected;

        public bool OverlapDetected => Volatile.Read(ref _overlapDetected) != 0;

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _activeWrites) > 1)
                Interlocked.Exchange(ref _overlapDetected, 1);

            try
            {
                await Task.Delay(1, cancellationToken);
                lock (this)
                    base.Write(buffer, offset, count);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWrites);
            }
        }
    }
}
