using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lib.GAB;
using Lib.GAB.Tools;
using Newtonsoft.Json.Linq;

namespace Lib.GAB.Tests;

public class ToolCallContextTests
{
    [Fact]
    public async Task ReportsUnrecognizedArgumentsToTheToolMethod()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var result = await registry.CallToolAsync("probe/add", new { a = 2, b = 3, enabledOnly = true, includeRects = false });

        Assert.Equal(5, result);
        var context = Assert.Single(tools.Seen);
        Assert.Equal("probe/add", context.ToolName);
        Assert.Equal(new[] { "enabledOnly", "includeRects" }, context.UnrecognizedArguments);
        Assert.True(context.HasUnrecognizedArguments);
        Assert.Equal(new[] { "a", "b" }, context.ParameterNames);
        Assert.Equal(new[] { "a", "b", "enabledOnly", "includeRects" }, context.Arguments.Keys);
        Assert.Equal(true, context.Arguments["enabledOnly"]);
    }

    [Fact]
    public async Task ReportsUnrecognizedArgumentsFromServerStyleJsonArguments()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var result = await registry.CallToolAsync("probe/add", JObject.Parse("{\"a\":1,\"b\":1,\"extra\":{\"x\":1}}"));

        Assert.Equal(2, result);
        var context = Assert.Single(tools.Seen);
        Assert.Equal(new[] { "extra" }, context.UnrecognizedArguments);
    }

    [Fact]
    public async Task CaseInsensitiveMatchesAreNotUnrecognized()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var result = await registry.CallToolAsync("probe/add", new { A = 4, B = 5 });

        Assert.Equal(9, result);
        var context = Assert.Single(tools.Seen);
        Assert.Empty(context.UnrecognizedArguments);
        Assert.False(context.HasUnrecognizedArguments);
        Assert.Equal(new[] { "A", "B" }, context.Arguments.Keys);
    }

    [Fact]
    public async Task NullArgumentsProduceAnEmptyContext()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var result = await registry.CallToolAsync("probe/echo");

        Assert.Equal("fallback", result);
        var context = Assert.Single(tools.Seen);
        Assert.Equal("probe/echo", context.ToolName);
        Assert.Empty(context.Arguments);
        Assert.Empty(context.UnrecognizedArguments);
    }

    [Fact]
    public async Task ContextIsNullOutsideToolCalls()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        Assert.Null(ToolCallContext.Current);

        await registry.CallToolAsync("probe/add", new { a = 1, b = 2, unknown = 3 });
        Assert.Null(ToolCallContext.Current);

        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.CallToolAsync("probe/fail", new { unknown = 1 }));
        Assert.Null(ToolCallContext.Current);
        Assert.Single(tools.Seen, context => context.ToolName == "probe/fail");
    }

    [Fact]
    public async Task ContextDoesNotLeakToTheCallerWhileAnAsyncToolIsSuspended()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var pending = registry.CallToolAsync("probe/wait", new { unknown = 1 });

        Assert.False(pending.IsCompleted);
        Assert.Null(ToolCallContext.Current);

        tools.Gate.SetResult(true);
        Assert.Equal("released", await pending);
        Assert.Null(ToolCallContext.Current);
        Assert.Equal(new[] { "unknown" }, Assert.Single(tools.Seen).UnrecognizedArguments);
    }

    [Theory]
    [InlineData("probe/task")]
    [InlineData("probe/task_of_t")]
    [InlineData("probe/value_task")]
    [InlineData("probe/value_task_of_t")]
    public async Task AsyncToolMethodsSeeTheContextAfterAwaiting(string toolName)
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var result = await registry.CallToolAsync(toolName, new { value = 7, guessed = "x" });

        // Non-generic Task/ValueTask results keep the pre-existing result behavior and are not asserted here.
        if (toolName.EndsWith("_of_t", StringComparison.Ordinal))
            Assert.Equal(7, result);
        Assert.Equal(2, tools.Seen.Count);
        Assert.All(tools.Seen, context =>
        {
            Assert.NotNull(context);
            Assert.Equal(toolName, context.ToolName);
            Assert.Equal(new[] { "guessed" }, context.UnrecognizedArguments);
        });
        Assert.Null(ToolCallContext.Current);
    }

    [Fact]
    public async Task ConcurrentCallsSeeTheirOwnContext()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        var first = registry.CallToolAsync("probe/wait", new { firstGuess = 1 });
        var second = registry.CallToolAsync("probe/wait", new { secondGuess = 2 });
        tools.Gate.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(2, tools.Seen.Count);
        Assert.Contains(tools.Seen, context => context.UnrecognizedArguments.Count == 1 && context.UnrecognizedArguments[0] == "firstGuess");
        Assert.Contains(tools.Seen, context => context.UnrecognizedArguments.Count == 1 && context.UnrecognizedArguments[0] == "secondGuess");
    }

    [Fact]
    public async Task NestedToolCallsRestoreTheOuterContext()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);
        tools.Registry = registry;

        await registry.CallToolAsync("probe/nested", new { outerGuess = 1 });

        Assert.Equal(3, tools.Seen.Count);
        Assert.Equal("probe/nested", tools.Seen[0].ToolName);
        Assert.Equal("probe/add", tools.Seen[1].ToolName);
        Assert.Equal(new[] { "innerGuess" }, tools.Seen[1].UnrecognizedArguments);
        Assert.Same(tools.Seen[0], tools.Seen[2]);
        Assert.Null(ToolCallContext.Current);
    }

    [Fact]
    public async Task ManuallyRegisteredHandlersDoNotGetAContext()
    {
        var registry = new ToolRegistry();
        ToolCallContext seen = new ToolCallContext("sentinel/value", null, null, null);
        registry.RegisterTool("manual/probe", _ =>
        {
            seen = ToolCallContext.Current;
            return Task.FromResult<object>("ok");
        });

        Assert.Equal("ok", await registry.CallToolAsync("manual/probe", new { anything = 1 }));
        Assert.Null(seen);
    }

    [Fact]
    public async Task UnknownArgumentsRemainIgnoredByBinding()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        Assert.Equal("fallback", await registry.CallToolAsync("probe/echo", new { messages = "typo" }));
        Assert.Equal("given", await registry.CallToolAsync("probe/echo", new { Message = "given", extra = 1 }));
        Assert.Equal(new[] { "messages" }, tools.Seen[0].UnrecognizedArguments);
        Assert.Equal(new[] { "extra" }, tools.Seen[1].UnrecognizedArguments);
    }

    [Fact]
    public async Task InvalidArgumentValuesStillFailBeforeTheContextIsEntered()
    {
        var tools = new ContextProbeTools();
        var registry = CreateRegistry(tools);

        await Assert.ThrowsAsync<ToolParameterBindingException>(() =>
            registry.CallToolAsync("probe/add", new { a = "not a number", b = 1, extra = 1 }));

        Assert.Empty(tools.Seen);
        Assert.Null(ToolCallContext.Current);
    }

    private static ToolRegistry CreateRegistry(ContextProbeTools tools)
    {
        var registry = new ToolRegistry();
        registry.RegisterToolsFromInstance(tools);
        return registry;
    }

    public class ContextProbeTools
    {
        public List<ToolCallContext> Seen { get; } = new List<ToolCallContext>();
        public TaskCompletionSource<bool> Gate { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public IToolRegistry Registry { get; set; }

        private void Record()
        {
            lock (Seen)
                Seen.Add(ToolCallContext.Current);
        }

        [Tool("probe/add")]
        public int Add(int a, int b)
        {
            Record();
            return a + b;
        }

        [Tool("probe/echo")]
        public string Echo(string message = "fallback")
        {
            Record();
            return message;
        }

        [Tool("probe/fail")]
        public void Fail()
        {
            Record();
            throw new InvalidOperationException("boom");
        }

        [Tool("probe/wait")]
        public async Task<string> Wait()
        {
            await Gate.Task;
            Record();
            return "released";
        }

        [Tool("probe/task")]
        public async Task TaskTool(int value)
        {
            Record();
            await Task.Delay(1);
            Record();
        }

        [Tool("probe/task_of_t")]
        public async Task<int> TaskOfTTool(int value)
        {
            Record();
            await Task.Delay(1);
            Record();
            return value;
        }

        [Tool("probe/value_task")]
        public async ValueTask ValueTaskTool(int value)
        {
            Record();
            await Task.Delay(1);
            Record();
        }

        [Tool("probe/value_task_of_t")]
        public async ValueTask<int> ValueTaskOfTTool(int value)
        {
            Record();
            await Task.Delay(1);
            Record();
            return value;
        }

        [Tool("probe/nested")]
        public async Task Nested()
        {
            Record();
            await Registry.CallToolAsync("probe/add", new { a = 1, b = 1, innerGuess = true });
            Record();
        }
    }
}
