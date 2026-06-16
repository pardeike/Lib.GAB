using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Lib.GAB.Tools;

namespace Lib.GAB.Tests;

[Collection("GabsEnvironment")]
public class GabsIntegrationTests
{
    [Fact]
    public void IsRunningUnderGabs_ReturnsFalse_WhenNoEnvironmentVariables()
    {
        // Arrange - Clear any existing environment variables
        Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
        Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
        Environment.SetEnvironmentVariable("GABP_TOKEN", null);
        Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

        // Act
        var result = Gabp.IsRunningUnderGabs();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsRunningUnderGabs_ReturnsTrue_WhenGabsEnvironmentVariablesAreSet()
    {
        try
        {
            // Arrange
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "test-game");
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", "12345");
            Environment.SetEnvironmentVariable("GABP_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

            // Act
            var result = Gabp.IsRunningUnderGabs();

            // Assert
            Assert.True(result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
        }
    }

    [Fact]
    public void IsRunningUnderGabs_ReturnsFalse_WhenPortIsNotNumeric()
    {
        try
        {
            // Arrange
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "invalid-port-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", "not-a-number");
            Environment.SetEnvironmentVariable("GABP_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

            // Act
            var result = Gabp.IsRunningUnderGabs();

            // Assert
            Assert.False(result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    public void IsRunningUnderGabs_ReturnsFalse_WhenPortIsOutOfRange(string port)
    {
        try
        {
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "invalid-port-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", port);
            Environment.SetEnvironmentVariable("GABP_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

            var result = Gabp.IsRunningUnderGabs();

            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
        }
    }

    [Fact]
    public async Task CreateGabsAwareServer_UsesGabsEnvironment_WhenAvailable()
    {
        try
        {
            // Arrange
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "test-game");
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", "12345");
            Environment.SetEnvironmentVariable("GABP_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

            // Act
            var server = Gabp.CreateGabsAwareServer("Test App", "1.0.0", fallbackPort: 9999);
            await server.StartAsync();

            // Assert
            Assert.NotNull(server);
            Assert.Equal(12345, server.Port);
            Assert.Equal("test-token", server.Token);
            
            await server.StopAsync();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
        }
    }

    [Fact]
    public async Task CreateGabsAwareServer_UsesFallbackPort_WhenGabsEnvironmentNotAvailable()
    {
        // Arrange - Ensure GABS environment variables are not set
        Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
        Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
        Environment.SetEnvironmentVariable("GABP_TOKEN", null);
        Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

        // Act
        var server = Gabp.CreateGabsAwareServer("Test App", "1.0.0", fallbackPort: 9999);
        await server.StartAsync();

        // Assert
        Assert.NotNull(server);
        Assert.Equal(9999, server.Port);
        Assert.NotEqual("test-token", server.Token); // Should have generated a new token
        
        await server.StopAsync();
    }

    [Fact]
    public async Task CreateGabsAwareServerWithInstance_RegistersTools()
    {
        try
        {
            // Arrange
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "test-game");
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", "12345");
            Environment.SetEnvironmentVariable("GABP_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);

            var toolsInstance = new TestTools();

            // Act
            var server = Gabp.CreateGabsAwareServerWithInstance("Test App", "1.0.0", toolsInstance, fallbackPort: 9999);
            await server.StartAsync();

            // Assert
            Assert.NotNull(server);
            Assert.Equal(12345, server.Port);
            Assert.Equal("test-token", server.Token);
            
            var tools = server.Tools.GetTools();
            Assert.Contains(tools, t => t.Name == "test/tool");
            
            await server.StopAsync();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
        }
    }

    [Fact]
    public async Task CreateGabsAwareServer_IgnoresBridgePath_WhenEnvironmentPortAndTokenAreMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "libgab-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var bridgePath = Path.Combine(tempDir, "bridge.json");
        var port = GetUnusedPort();
        await File.WriteAllTextAsync(bridgePath, $@"{{""port"":{port},""token"":""bridge-token"",""gameId"":""bridge-game""}}");

        try
        {
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "bridge-game");
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", bridgePath);

            var server = Gabp.CreateGabsAwareServer("Test App", "1.0.0", fallbackPort: 9999);
            await server.StartAsync();

            Assert.Equal(9999, server.Port);
            Assert.NotEqual(port, server.Port);
            Assert.NotEqual("bridge-token", server.Token);

            await server.StopAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IsRunningUnderGabs_ReturnsFalse_WhenOnlyBridgePathIsAvailable()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "libgab-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var bridgePath = Path.Combine(tempDir, "bridge.json");
        var port = GetUnusedPort();
        File.WriteAllText(bridgePath, $@"{{""port"":{port},""token"":""bridge-token"",""gameId"":""bridge-game""}}");

        try
        {
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "bridge-game");
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", bridgePath);

            Assert.False(Gabp.IsRunningUnderGabs());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
            Environment.SetEnvironmentVariable("GABP_TOKEN", null);
            Environment.SetEnvironmentVariable("GABS_BRIDGE_PATH", null);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private class TestTools
    {
        [Tool("test/tool", Description = "A test tool")]
        public object TestTool()
        {
            return new { success = true };
        }
    }

    private static int GetUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition("GabsEnvironment", DisableParallelization = true)]
public class GabsEnvironmentCollection
{
}
