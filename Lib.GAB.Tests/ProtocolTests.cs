using System;
using System.Collections.Generic;
using System.Text.Json;
using Lib.GAB.Protocol;

namespace Lib.GAB.Tests;

public class ProtocolTests
{
    [Fact]
    public void CanSerializeGabpRequest()
    {
        // Arrange
        var request = new GabpRequest
        {
            Method = "session/hello",
            Params = new SessionHelloParams
            {
                Token = "test-token",
                BridgeVersion = "1.0.0",
                Platform = "linux",
                LaunchId = "test-launch-id"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<GabpRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("gabp/1", deserialized.Version);
        Assert.Equal("request", deserialized.Type);
        Assert.Equal("session/hello", deserialized.Method);
        Assert.NotNull(deserialized.Params);
    }

    [Fact]
    public void CanSerializeGabpResponse()
    {
        // Arrange
        var response = new GabpResponse
        {
            Result = new SessionWelcomeResult
            {
                AgentId = "test-agent",
                App = new AppInfo { Name = "Test App", Version = "1.0.0" },
                Capabilities = new Capabilities
                {
                    Tools = new List<string> { "test/tool" },
                    Events = new List<string> { "test/event" },
                    Resources = new List<string>()
                },
                SchemaVersion = "1.0"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<GabpResponse>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("gabp/1", deserialized.Version);
        Assert.Equal("response", deserialized.Type);
        Assert.NotNull(deserialized.Result);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public void CanSerializeGabpEvent()
    {
        // Arrange
        var eventMessage = new GabpEvent
        {
            Channel = "player/move",
            Sequence = 42,
            Payload = new { playerId = "steve", position = new { x = 100, y = 64, z = 200 } },
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(eventMessage);
        var deserialized = JsonSerializer.Deserialize<GabpEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("gabp/1", deserialized.Version);
        Assert.Equal("event", deserialized.Type);
        Assert.Equal("player/move", deserialized.Channel);
        Assert.Equal(42, deserialized.Sequence);
        Assert.NotNull(deserialized.Payload);
        Assert.NotNull(deserialized.Timestamp);
    }

    [Fact]
    public void CanSerializeGabpError()
    {
        // Arrange
        var response = new GabpResponse
        {
            Error = new GabpError
            {
                Code = GabpErrorCodes.MethodNotFound,
                Message = "Method not found",
                Data = new { method = "unknown/method" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<GabpResponse>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Result);
        Assert.NotNull(deserialized.Error);
        Assert.Equal(GabpErrorCodes.MethodNotFound, deserialized.Error.Code);
        Assert.Equal("Method not found", deserialized.Error.Message);
    }
}