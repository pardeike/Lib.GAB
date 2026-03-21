using System;
using System.Threading.Tasks;
using Lib.GAB.Server;
using Lib.GAB.Tools;
using GabpApi = Lib.GAB.Gabp;

// Minimal host-side GABP server:
// - works standalone or under GABS
// - exposes two tools and one event channel
// - keeps the lifecycle flat so it can be copied into an existing game/app host

var tools = new HelloWorldTools();
// One factory works for local smoke tests and for real launches under GABS
using var server = GabpApi.CreateGabsAwareServerWithInstance("HelloWorldHost", "1.0.0", tools);

// Keep one event channel in the sample so the event side of GABP is visible too
server.Events.RegisterChannel("host/lifecycle", "Lifecycle events from the hello world bridge host");

await server.StartAsync();
await EmitLifecycleAsync(server, "started");

ShowStartup(server);
// In a real host, replace this with your game's or app's normal lifetime hook
Console.ReadLine();

await EmitLifecycleAsync(server, "stopping");
await server.StopAsync();

static void ShowStartup(GabpServer server)
{
    var runtimeMode = GabpApi.IsRunningUnderGabs()
        ? "Running under GABS. Configuration came from the GABS environment."
        : "Running standalone. Use the printed port and token to connect a bridge directly.";

    Console.WriteLine($"""
        GABP server started on port {server.Port}
        Token: {server.Token}

        {runtimeMode}
        Available tools: hello/ping, hello/echo
        Available event channel: host/lifecycle
        Press Enter to stop.
        """);
}

static Task EmitLifecycleAsync(GabpServer server, string state)
{
    return server.Events.EmitEventAsync("host/lifecycle", new
    {
        state,
        timestampUtc = DateTime.UtcNow.ToString("O")
    });
}

public sealed class HelloWorldTools
{
    [Tool(
        "hello/ping",
        Description = "Confirm that the host is alive.",
        ResultDescription = "A human-readable status message and a UTC timestamp from the running host.")]
    [ToolResponse("message", Type = "string", Description = "Human-readable status from the host")]
    [ToolResponse("timestampUtc", Type = "string", Description = "Current UTC timestamp from the host")]
    public object Ping()
    {
        return new
        {
            message = "Hello from Lib.GAB",
            timestampUtc = DateTime.UtcNow.ToString("O")
        };
    }

    [Tool(
        "hello/echo",
        Description = "Echo a message back to the caller.",
        ResultDescription = "The message returned by the host.")]
    [ToolResponse("message", Type = "string", Description = "Echoed message from the host")]
    public object Echo(
        [ToolParameter(Description = "Message to echo back", Required = false, DefaultValue = "Hello GABS")]
        string message = "Hello GABS")
    {
        return new { message };
    }
}
