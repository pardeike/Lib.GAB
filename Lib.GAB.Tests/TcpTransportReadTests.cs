using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lib.GAB.Protocol;
using Lib.GAB.Transport;
using Newtonsoft.Json;
using Xunit;

namespace Lib.GAB.Tests;

public class TcpTransportReadTests
{
    private const string UnicodeText = "Mitasño é Ω Щ ユニティ 🚀";

    [Theory]
    [InlineData("ASCII")]
    [InlineData(UnicodeText)]
    public async Task EverySplitPreservesCompleteFramesAndTheirOrder(string text)
    {
        var first = ResponseFrame(text);
        var frames = first.Concat(ResponseFrame("next")).ToArray();
        if (text == UnicodeText)
        {
            Assert.Contains((byte)0xc3, first); // Two-byte character.
            Assert.Contains((byte)0xe3, first); // Three-byte character.
            Assert.Contains((byte)0xf0, first); // Four-byte character, emitted without JSON escaping.
        }

        // Feed the actual receive parser at every boundary, without relying on TCP packet timing.
        for (var split = 0; split <= frames.Length; split++)
        {
            using var transport = new TcpTransport();
            using var pending = new MemoryStream();
            var received = new List<GabpMessage>();
            transport.MessageReceived += (_, args) => received.Add(args.Message);
            pending.Write(frames, 0, split);
            await transport.ProcessMessagesAsync(null, pending, CancellationToken.None);
            var expected = split == frames.Length ? 2 : split >= first.Length ? 1 : 0;
            Assert.Equal(expected, received.Count);

            pending.Write(frames, split, frames.Length - split);
            await transport.ProcessMessagesAsync(null, pending, CancellationToken.None);
            Assert.Equal(new[] { text, "next" }, received.Select(message => Assert.IsType<GabpResponse>(message).Result));
            Assert.Equal(0, pending.Length);
        }
    }

    [Fact]
    public async Task ByteAtATimeWaitsForTheCompleteUnicodeBody()
    {
        using var transport = new TcpTransport();
        using var pending = new MemoryStream();
        var received = new List<GabpMessage>();
        transport.MessageReceived += (_, args) => received.Add(args.Message);
        var frame = ResponseFrame(UnicodeText);
        for (var index = 0; index < frame.Length; index++)
        {
            pending.WriteByte(frame[index]);
            await transport.ProcessMessagesAsync(null, pending, CancellationToken.None);
            Assert.Equal(index == frame.Length - 1 ? 1 : 0, received.Count);
        }
        Assert.Equal(UnicodeText, Assert.IsType<GabpResponse>(Assert.Single(received)).Result);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"type\":\"unknown\"}")]
    [InlineData("")]
    public async Task UnparseableBodyDoesNotConsumeTheFollowingFrame(string json)
    {
        using var transport = new TcpTransport();
        using var pending = new MemoryStream();
        var received = new List<GabpMessage>();
        transport.MessageReceived += (_, args) => received.Add(args.Message);
        var frames = Frame(Encoding.UTF8.GetBytes(json)).Concat(ResponseFrame("next")).ToArray();
        pending.Write(frames, 0, frames.Length);
        await transport.ProcessMessagesAsync(null, pending, CancellationToken.None);
        Assert.Equal("next", Assert.IsType<GabpResponse>(Assert.Single(received)).Result);
        Assert.Equal(0, pending.Length);
    }

    [Fact]
    public async Task MalformedUtf8RetainsReplacementFallbackAndFrameBoundary()
    {
        using var transport = new TcpTransport();
        using var pending = new MemoryStream();
        var received = new List<GabpMessage>();
        transport.MessageReceived += (_, args) => received.Add(args.Message);
        var body = Encoding.ASCII.GetBytes("{\"type\":\"response\",\"result\":\"?\"}");
        body[Array.IndexOf(body, (byte)'?')] = 0xff;
        var frames = Frame(body).Concat(ResponseFrame("next")).ToArray();
        pending.Write(frames, 0, frames.Length);
        await transport.ProcessMessagesAsync(null, pending, CancellationToken.None);
        Assert.Equal(new[] { "\ufffd", "next" }, received.Select(message => Assert.IsType<GabpResponse>(message).Result));
        Assert.Equal(0, pending.Length);
    }

    [Theory]
    [InlineData("Content-Type: application/json")]
    [InlineData("Content-Length: invalid")]
    [InlineData("Content-Length: 100000")]
    public async Task UnusableOrIncompleteLengthWaitsWithoutDispatch(string header)
    {
        using var transport = new TcpTransport();
        using var pending = new MemoryStream();
        var received = new List<GabpMessage>();
        transport.MessageReceived += (_, args) => received.Add(args.Message);
        var bytes = Encoding.ASCII.GetBytes(header + "\r\n\r\n{}");
        pending.Write(bytes, 0, bytes.Length);
        await transport.ProcessMessagesAsync(null, pending, CancellationToken.None);
        Assert.Empty(received);
        Assert.Equal(bytes, pending.ToArray());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483647")]
    public async Task OutOfRangeLengthStillFailsTheReceiveLoop(string length)
    {
        using var transport = new TcpTransport();
        using var pending = new MemoryStream();
        var bytes = Encoding.ASCII.GetBytes($"Content-Length: {length}\r\n\r\n");
        pending.Write(bytes, 0, bytes.Length);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.ProcessMessagesAsync(null, pending, CancellationToken.None));
    }

    private static byte[] ResponseFrame(string text) => Frame(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
    {
        v = "gabp/1", id = "550e8400-e29b-41d4-a716-4466554400a0", type = "response", result = text
    })));

    private static byte[] Frame(byte[] body) => Encoding.ASCII.GetBytes(
        $"content-length: {body.Length}\r\nContent-Type: application/json\r\n\r\n").Concat(body).ToArray();
}
