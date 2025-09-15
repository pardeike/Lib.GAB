using System;
using System.Threading.Tasks;
using Lib.GAB.Tools;

namespace Lib.GAB.Tests;

public class GabsIntegrationTests
{
    [Fact]
    public void IsRunningUnderGabs_ReturnsFalse_WhenNoEnvironmentVariables()
    {
        // Arrange - Clear any existing environment variables
        Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
        Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
        Environment.SetEnvironmentVariable("GABP_TOKEN", null);

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
        }
    }

    [Fact]
    public void IsRunningUnderGabs_ReturnsFalse_WhenPortIsNotNumeric()
    {
        try
        {
            // Arrange
            Environment.SetEnvironmentVariable("GABS_GAME_ID", "test-game");
            Environment.SetEnvironmentVariable("GABP_SERVER_PORT", "not-a-number");
            Environment.SetEnvironmentVariable("GABP_TOKEN", "test-token");

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
        }
    }

    [Fact]
    public async Task CreateGabsAwareServer_UsesFallbackPort_WhenGabsEnvironmentNotAvailable()
    {
        // Arrange - Ensure GABS environment variables are not set
        Environment.SetEnvironmentVariable("GABS_GAME_ID", null);
        Environment.SetEnvironmentVariable("GABP_SERVER_PORT", null);
        Environment.SetEnvironmentVariable("GABP_TOKEN", null);

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
}