using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Lib.GAB;
using Lib.GAB.Tools;

namespace Lib.GAB.Tests;

public class ToolRegistryTests
{
    private static readonly object TraceLock = new object();

    [Fact]
    public async Task MatchesParametersCaseInsensitively()
    {
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new ParameterBindingTestTools());

        var result = await server.Tools.CallToolAsync("math/add", new { A = 5, B = 3 });

        Assert.Equal(8, result);
    }

    [Fact]
    public void CaseInsensitiveMatchesDoNotLogUnrecognizedWarnings()
    {
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new ParameterBindingTestTools());

        var traceOutput = CaptureTraceOutput(() =>
        {
            var result = server.Tools.CallToolAsync("math/add", new { A = 5, B = 3 }).GetAwaiter().GetResult();
            Assert.Equal(8, result);
        });

        Assert.Contains("parameter 'A' matched 'a' via case-insensitive fallback", traceOutput);
        Assert.Contains("parameter 'B' matched 'b' via case-insensitive fallback", traceOutput);
        Assert.DoesNotContain("unrecognized parameter 'A'", traceOutput);
        Assert.DoesNotContain("unrecognized parameter 'B'", traceOutput);
    }

    [Fact]
    public void UnknownParametersStillLogWarnings()
    {
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new ParameterBindingTestTools());

        var traceOutput = CaptureTraceOutput(() =>
        {
            var result = server.Tools.CallToolAsync("math/add", new { a = 5, b = 3, c = 1 }).GetAwaiter().GetResult();
            Assert.Equal(8, result);
        });

        Assert.Contains("unrecognized parameter 'c'", traceOutput);
    }

    [Fact]
    public void CapturesOptionalResultDescriptionFromToolMetadata()
    {
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new ParameterBindingTestTools());

        var tool = Assert.Single(server.Tools.GetTools());

        Assert.Equal("Returns the sum of the two inputs as an integer.", tool.ResultDescription);
    }

    [Fact]
    public void OptionalMethodParametersRemainOptionalInToolMetadata()
    {
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new OptionalParameterTestTools());

        var tool = Assert.Single(server.Tools.GetTools());
        var optional = Assert.Single(tool.Parameters, parameter => parameter.Name == "message");

        Assert.False(optional.Required);
        Assert.Equal("fallback", optional.DefaultValue);
    }

    [Fact]
    public async Task UsesToolParameterAttributeDefaultValueWhenArgumentIsMissing()
    {
        var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(new AttributeDefaultValueTestTools());

        var result = await server.Tools.CallToolAsync("math/increment", new { });

        Assert.Equal(11, result);
    }

    private static string CaptureTraceOutput(Action action)
    {
        lock (TraceLock)
        {
            var previousAutoFlush = Trace.AutoFlush;
            using var writer = new StringWriter();
            using var listener = new TextWriterTraceListener(writer);
            Trace.AutoFlush = true;
            Trace.Listeners.Add(listener);

            try
            {
                action();
                listener.Flush();
                return writer.ToString();
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                Trace.AutoFlush = previousAutoFlush;
            }
        }
    }

    public class ParameterBindingTestTools
    {
        [Tool("math/add", Description = "Add two numbers", ResultDescription = "Returns the sum of the two inputs as an integer.")]
        public int Add([ToolParameter(Description = "First number")] int a,
            [ToolParameter(Description = "Second number")] int b)
        {
            return a + b;
        }
    }

    public class OptionalParameterTestTools
    {
        [Tool("system/echo", Description = "Echo text with an optional suffix")]
        public string Echo([ToolParameter(Description = "Base message")] string message = "fallback")
        {
            return message;
        }
    }

    public class AttributeDefaultValueTestTools
    {
        [Tool("math/increment", Description = "Increment a number using attribute-level defaults")]
        public int Increment([ToolParameter(Description = "Value to increment", Required = false, DefaultValue = 10)] int value)
        {
            return value + 1;
        }
    }
}
