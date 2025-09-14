using Lib.GAB.Protocol;
using Lib.GAB.Tools;

namespace Lib.GAB.Tests;

public class GabpServerTests
{
    [Fact]
    public void CanCreateServer()
    {
        // Arrange & Act
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");

        // Assert
        Assert.NotNull(server);
        Assert.NotNull(server.Tools);
        Assert.NotNull(server.Events);
    }

    [Fact]
    public void CanRegisterTool()
    {
        // Arrange
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");

        // Act
        server.Tools.RegisterTool("test/hello", _ => Task.FromResult<object?>("Hello World"));

        // Assert
        Assert.True(server.Tools.HasTool("test/hello"));
        var tools = server.Tools.GetTools();
        Assert.Contains(tools, t => t.Name == "test/hello");
    }

    [Fact]
    public async Task CanCallTool()
    {
        // Arrange
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterTool("test/echo", args => Task.FromResult<object?>(args));

        // Act
        var result = await server.Tools.CallToolAsync("test/echo", "test message");

        // Assert
        Assert.Equal("test message", result);
    }

    [Fact]
    public void CanRegisterToolsFromInstance()
    {
        // Arrange
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        var testInstance = new TestToolsClass();

        // Act
        server.Tools.RegisterToolsFromInstance(testInstance);

        // Assert
        Assert.True(server.Tools.HasTool("math/add"));
        Assert.True(server.Tools.HasTool("string/upper"));
    }

    [Fact]  
    public async Task CanCallInstanceTool()
    {
        // Arrange
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        var testInstance = new TestToolsClass();
        server.Tools.RegisterToolsFromInstance(testInstance);

        // Act
        var result = await server.Tools.CallToolAsync("math/add", new { a = 5, b = 3 });

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void CanRegisterEventChannels()
    {
        // Arrange
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");

        // Act
        server.Events.RegisterChannel("test/events", "Test event channel");

        // Assert
        var channels = server.Events.GetAvailableChannels();
        Assert.Contains("test/events", channels);
    }
}

public class TestToolsClass
{
    [Tool("math/add", Description = "Add two numbers")]
    public int Add([ToolParameter(Description = "First number")] int a, 
                   [ToolParameter(Description = "Second number")] int b)
    {
        return a + b;
    }

    [Tool("string/upper", Description = "Convert string to uppercase")]
    public string ToUpper([ToolParameter(Description = "Input string")] string input)
    {
        return input.ToUpper();
    }
}