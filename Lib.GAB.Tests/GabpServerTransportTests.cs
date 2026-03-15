using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Lib.GAB.Tools;
using Xunit;

namespace Lib.GAB.Tests;

public class GabpServerTransportTests
{
    [Fact]
    public async Task ToolsCallAcceptsParametersCompatibilityField()
    {
        using var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new TransportTestTools());

        await server.StartAsync();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", server.Port);
            using var stream = client.GetStream();

            await SendFrameAsync(stream, new
            {
                v = "gabp/1",
                id = "550e8400-e29b-41d4-a716-446655440000",
                type = "request",
                method = "session/hello",
                @params = new
                {
                    token = server.Token,
                    bridgeVersion = "1.0.0",
                    platform = "linux",
                    launchId = "550e8400-e29b-41d4-a716-446655440001"
                }
            });

            var welcome = await ReadFrameAsync(stream);
            using var welcomeDoc = JsonDocument.Parse(welcome);
            Assert.Equal("response", welcomeDoc.RootElement.GetProperty("type").GetString());
            Assert.True(welcomeDoc.RootElement.TryGetProperty("result", out var welcomeResult));

            var capabilities = welcomeResult.GetProperty("capabilities");
            Assert.True(capabilities.TryGetProperty("methods", out var methods));
            Assert.Contains("math/add", methods.EnumerateArray().Select(entry => entry.GetString()));
            Assert.False(capabilities.TryGetProperty("tools", out _));

            await SendFrameAsync(stream, new
            {
                v = "gabp/1",
                id = "550e8400-e29b-41d4-a716-446655440010",
                type = "request",
                method = "tools/call",
                @params = new
                {
                    name = "math/add",
                    parameters = new
                    {
                        a = 5,
                        b = 3
                    }
                }
            });

            var response = await ReadFrameAsync(stream);
            using var responseDoc = JsonDocument.Parse(response);
            Assert.Equal("response", responseDoc.RootElement.GetProperty("type").GetString());
            Assert.Equal(8, responseDoc.RootElement.GetProperty("result").GetInt32());
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task SessionHelloReturnsCanonicalMethodsCapabilities()
    {
        using var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new TransportTestTools());

        await server.StartAsync();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", server.Port);
            using var stream = client.GetStream();

            await SendFrameAsync(stream, new
            {
                v = "gabp/1",
                id = "550e8400-e29b-41d4-a716-446655440020",
                type = "request",
                method = "session/hello",
                @params = new
                {
                    token = server.Token,
                    bridgeVersion = "1.0.0",
                    platform = "linux",
                    launchId = "550e8400-e29b-41d4-a716-446655440021"
                }
            });

            var welcome = await ReadFrameAsync(stream);
            using var welcomeDoc = JsonDocument.Parse(welcome);
            var result = welcomeDoc.RootElement.GetProperty("result");
            var capabilities = result.GetProperty("capabilities");

            Assert.Equal("1.0", result.GetProperty("schemaVersion").GetString());
            Assert.True(capabilities.TryGetProperty("methods", out var methods));
            Assert.Contains("math/add", methods.EnumerateArray().Select(entry => entry.GetString()));
            Assert.False(capabilities.TryGetProperty("tools", out _));
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task ToolsListEmitsCanonicalAndCompatibilitySchemas()
    {
        using var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new TransportTestTools());

        await server.StartAsync();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", server.Port);
            using var stream = client.GetStream();

            await SendFrameAsync(stream, new
            {
                v = "gabp/1",
                id = "550e8400-e29b-41d4-a716-446655440030",
                type = "request",
                method = "session/hello",
                @params = new
                {
                    token = server.Token,
                    bridgeVersion = "1.0.0",
                    platform = "linux",
                    launchId = "550e8400-e29b-41d4-a716-446655440031"
                }
            });

            _ = await ReadFrameAsync(stream);

            await SendFrameAsync(stream, new
            {
                v = "gabp/1",
                id = "550e8400-e29b-41d4-a716-446655440032",
                type = "request",
                method = "tools/list",
                @params = new { }
            });

            var toolsList = await ReadFrameAsync(stream);
            using var document = JsonDocument.Parse(toolsList);
            var tool = document.RootElement
                .GetProperty("result")
                .GetProperty("tools")
                .EnumerateArray()
                .Single(entry => entry.GetProperty("name").GetString() == "math/add");

            Assert.Equal("Add Numbers", tool.GetProperty("title").GetString());
            Assert.Equal("Add two numbers", tool.GetProperty("description").GetString());

            var inputSchema = tool.GetProperty("inputSchema");
            Assert.Equal("object", inputSchema.GetProperty("type").GetString());
            Assert.False(inputSchema.GetProperty("additionalProperties").GetBoolean());
            Assert.True(inputSchema.TryGetProperty("properties", out var properties));
            Assert.True(properties.TryGetProperty("a", out var paramA));
            Assert.Equal("integer", paramA.GetProperty("type").GetString());
            Assert.True(properties.TryGetProperty("b", out var paramB));
            Assert.Equal("integer", paramB.GetProperty("type").GetString());

            var required = inputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains("a", required);
            Assert.Contains("b", required);

            Assert.True(tool.TryGetProperty("parameters", out var parameters));
            Assert.Equal(2, parameters.GetArrayLength());

            Assert.True(tool.TryGetProperty("outputSchema", out var outputSchema));
            Assert.Equal(JsonValueKind.Object, outputSchema.ValueKind);
            Assert.True(tool.TryGetProperty("requiresAuth", out var requiresAuth));
            Assert.True(requiresAuth.GetBoolean());
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task SessionHelloAcceptsSingleContentLengthHeader()
    {
        using var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new TransportTestTools());

        await server.StartAsync();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", server.Port);
            using var stream = client.GetStream();

            await SendFrameAsync(stream, new
            {
                v = "gabp/1",
                id = "550e8400-e29b-41d4-a716-446655440040",
                type = "request",
                method = "session/hello",
                @params = new
                {
                    token = server.Token,
                    bridgeVersion = "1.0.0",
                    platform = "linux",
                    launchId = "550e8400-e29b-41d4-a716-446655440041"
                }
            }, includeContentType: false);

            var welcome = await ReadFrameAsync(stream);
            using var welcomeDoc = JsonDocument.Parse(welcome);
            Assert.Equal("response", welcomeDoc.RootElement.GetProperty("type").GetString());
            Assert.True(welcomeDoc.RootElement.TryGetProperty("result", out var welcomeResult));
            Assert.Equal("1.0", welcomeResult.GetProperty("schemaVersion").GetString());
        }
        finally
        {
            await server.StopAsync();
        }
    }

    private static async Task SendFrameAsync(NetworkStream stream, object payload, bool includeContentType = true)
    {
        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);
        var headerText = includeContentType
            ? $"Content-Length: {body.Length}\r\nContent-Type: application/json\r\n\r\n"
            : $"Content-Length: {body.Length}\r\n\r\n";
        var header = Encoding.ASCII.GetBytes(headerText);

        await stream.WriteAsync(header, 0, header.Length);
        await stream.WriteAsync(body, 0, body.Length);
        await stream.FlushAsync();
    }

    private static async Task<string> ReadFrameAsync(NetworkStream stream)
    {
        var headerBuffer = new StringBuilder();
        var oneByte = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(oneByte, 0, 1);
            if (read == 0)
            {
                throw new InvalidOperationException("Connection closed while reading frame header.");
            }

            headerBuffer.Append((char)oneByte[0]);
            if (headerBuffer.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                break;
            }
        }

        var header = headerBuffer.ToString();
        const string contentLengthHeader = "Content-Length:";
        var start = header.IndexOf(contentLengthHeader, StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, "Missing Content-Length header.");

        start += contentLengthHeader.Length;
        var end = header.IndexOf("\r\n", start, StringComparison.Ordinal);
        var lengthText = header.Substring(start, end - start).Trim();
        var contentLength = int.Parse(lengthText);

        var body = new byte[contentLength];
        var offset = 0;
        while (offset < contentLength)
        {
            var read = await stream.ReadAsync(body, offset, contentLength - offset);
            if (read == 0)
            {
                throw new InvalidOperationException("Connection closed while reading frame body.");
            }

            offset += read;
        }

        return Encoding.UTF8.GetString(body);
    }

    private sealed class TransportTestTools
    {
        [Tool("math/add", Title = "Add Numbers", Description = "Add two numbers")]
        public int Add(
            [ToolParameter(Description = "First number")] int a,
            [ToolParameter(Description = "Second number")] int b)
        {
            return a + b;
        }
    }
}
