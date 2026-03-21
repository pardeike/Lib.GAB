# Lib.GAB

A .NET library for teams embedding a GABP (Game Agent Bridge Protocol) server into their own products.

## Overview

Lib.GAB is not an end-user bridge client and not a standalone operator-facing server product. It is a host-side library for products like RimBridgeServer, custom game mods, automation bridges, and embedded control surfaces that need to expose a GABP endpoint from inside a running game or application.

It builds on the shared `Gabp.Runtime` wire-model package for canonical protocol constants and message contracts, while keeping the integration surface simple: register tools, register event channels, publish optional attention, and start the server.

For the 1.0.0 release highlights and upgrade notes, see [`RELEASE_NOTES.md`](RELEASE_NOTES.md).

## Who This Is For

Use Lib.GAB if you are building a product that needs to:

- expose product- or game-specific tools over GABP
- aggregate tools contributed by multiple mods, plugins, or assemblies into one bridge surface
- advertise structured tool metadata to downstream bridges and agents
- emit events and optional blocking attention from inside the host process
- run inside environments such as Unity/Mono, .NET Framework, or modern .NET hosts

Lib.GAB is the transport and host ergonomics layer. Your product still owns its own domain behavior:

- capability design and naming
- game or application state access
- logging, diagnostics, and attention policy
- packaging and deployment into the final host product

## Installation

```bash
dotnet add package Lib.GAB
```

Reference it from the host-side component that actually exposes your GABP endpoint. `Lib.GAB` brings in `Gabp.Runtime` transitively, so consumers do not need a separate direct package reference to the runtime package.

## 5-Minute Hello World

If you just want to prove the flow end to end, start with the minimal sample in [`Lib.GAB.Example/Program.cs`](Lib.GAB.Example/Program.cs).

From a fresh clone:

```bash
dotnet run --project Lib.GAB.Example
```

That sample is intentionally small:

- one top-level host flow in `Program.cs`
- two tools: `hello/ping` and `hello/echo`
- one event channel: `host/lifecycle`
- one server creation path: `Gabp.CreateGabsAwareServerWithInstance(...)`
- two tiny helper methods for banner output and lifecycle event emission

Why this is the recommended starting point:

- it runs standalone, so you get an immediate success path without game-specific plumbing
- it also runs under GABS without code changes, because `CreateGabsAwareServerWithInstance(...)` picks up `GABS_GAME_ID`, `GABP_SERVER_PORT`, and `GABP_TOKEN` automatically
- the top-level flow can be copied into a Unity mod, a .NET Framework host, or any other .NET process and then adapted to whatever startup hook that product already has

When you run it manually, it prints the port and token you need for a direct bridge connection. When GABS launches it, the same code uses the GABS-provided configuration instead.

If you are embedding into an existing game or app, the first adaptation is usually just:

1. Copy the `Program.cs` server setup and the `HelloWorldTools` class into your project.
2. Move the `CreateGabsAwareServerWithInstance(...)` and `RegisterChannel(...)` lines into your existing startup hook.
3. Keep the same `StartAsync()` and `StopAsync()` calls, but wire them into your product lifecycle instead of `Console.ReadLine()`.
4. Replace `hello/ping` and `hello/echo` with product-specific tools.

## Features

- **GABP 1.0 Compliant**: Full implementation of the GABP specification
- **TCP Transport**: Listens on 127.0.0.1 with flexible port configuration
- **Tool Registration**: Manual and attribute-based tool registration
- **Schema-Aware Tool Metadata**: `tools/list` emits canonical `title`, `inputSchema`, and `outputSchema` descriptors
- **Event System**: Real-time event broadcasting to connected bridges
- **Optional Attention Support**: Exposes `attention/current`, `attention/ack`, and attention lifecycle events when enabled
- **Session Management**: Token-based authentication and capability negotiation
- **Shared Runtime Types**: Reuses `Gabp.Runtime` for protocol constants and request models
- **Easy Integration**: Simple API for quick setup and customization
- **Runtime Compatibility**: Ships `netstandard2.0` and `net10.0` assets, including Unity/Mono-based hosts such as RimWorld

## Product Builder Notes

If you are building a product in the style of RimBridgeServer, Lib.GAB gives you the host-facing mechanics:

- session establishment and authentication token handling
- canonical `session/hello`, `tools/list`, and `tools/call` behavior
- event subscription and event emission
- optional attention lifecycle endpoints and events
- attribute-driven or manual tool registration

Typical responsibilities you keep in your own product:

- mapping game or application operations into stable tool aliases
- discovering and vetting tools contributed by other mods, plugins, or extension assemblies
- deciding which async failures should open or clear attention
- deciding how much result metadata to expose through `ResultDescription` and `[ToolResponse]`
- integrating with your own launcher, bridge supervisor, or orchestration layer

If your product works like RimBridgeServer and republishes tools from other mods, that is a good fit for Lib.GAB. The library exposes the final host-side GABP surface, while your product still owns extension discovery, trust boundaries, naming rules, and lifecycle management for those contributed tools.

## GABS Integration

Lib.GAB integrates with [GABS](https://github.com/pardeike/GABS) (Game Agent Bridge Server) when your host product is launched under a supervising bridge/orchestration environment.

### Automatic GABS Detection

When your host process is launched by GABS, Lib.GAB automatically detects the GABS environment and configures itself appropriately:

```csharp
// Automatically detects and uses GABS configuration if available
// Falls back to standard configuration if not running under GABS
var server = Gabp.CreateGabsAwareServer("My Host Product", "1.0.0");

// With tools from a class instance
var hostTools = new HostTools();
var server = Gabp.CreateGabsAwareServerWithInstance("My Host Product", "1.0.0", hostTools);

await server.StartAsync();
```

### GABS Environment Variables

GABS provides configuration through environment variables:
- `GABS_GAME_ID`: Game identifier from GABS configuration
- `GABP_SERVER_PORT`: Port your host should listen on as a GABP server
- `GABP_TOKEN`: Authentication token for GABS connections

### Checking GABS Environment

You can check if your application is running under GABS:

```csharp
if (Gabp.IsRunningUnderGabs())
{
    Console.WriteLine("Running under GABS control");
    // Use GABS-aware server creation
    var server = Gabp.CreateGabsAwareServer("My Host Product", "1.0.0");
}
else
{
    Console.WriteLine("Running standalone");
    // Use traditional server creation
    var server = Gabp.CreateSimpleServer("My Host Product", "1.0.0");
}
```

### Manual GABS Configuration

For advanced scenarios, you can manually configure GABS settings:

```csharp
// Read GABS environment manually
var gameId = Environment.GetEnvironmentVariable("GABS_GAME_ID");
var port = int.Parse(Environment.GetEnvironmentVariable("GABP_SERVER_PORT"));
var token = Environment.GetEnvironmentVariable("GABP_TOKEN");

var server = Gabp.CreateServerWithExternalConfig("My Host Product", "1.0.0", port, token, gameId);
```

### Practical Usage Example

Here is a host-side integration example for a bridge product that should work both standalone and with GABS:

```csharp
using System;
using System.Threading.Tasks;
using Lib.GAB;
using Lib.GAB.Tools;

public class EmbeddedBridgeHost
{
    private GabpServer _server;

    public async Task InitializeAsync()
    {
        // Create a bridge host that automatically adapts to the environment
        _server = Gabp.CreateGabsAwareServerWithInstance("My Embedded Bridge", "1.0.0", this);
        
        // Register event channels
        _server.Events.RegisterChannel("player/move", "Player movement events");
        _server.Events.RegisterChannel("game/status", "Game status updates");
        
        await _server.StartAsync();
        
        if (Gabp.IsRunningUnderGabs())
        {
            Console.WriteLine($"Embedded bridge connected to GABS on port {_server.Port}");
        }
        else
        {
            Console.WriteLine($"Embedded bridge running standalone on port {_server.Port}");
            Console.WriteLine($"Bridge token: {_server.Token}");
        }
    }

    [Tool("player/teleport", Description = "Teleport player to coordinates")]
    public async Task<object> TeleportPlayer(
        [ToolParameter(Description = "Player name")] string player,
        [ToolParameter(Description = "X coordinate")] double x,
        [ToolParameter(Description = "Y coordinate")] double y,
        [ToolParameter(Description = "Z coordinate")] double z)
    {
        // Your product-specific or game-specific operation here
        await Game.TeleportPlayerAsync(player, x, y, z);
        
        // Notify about the teleport
        await _server.Events.EmitEventAsync("player/move", new
        {
            player,
            position = new { x, y, z },
            reason = "teleport"
        });
        
        return new { success = true, player, position = new { x, y, z } };
    }

    public async Task ShutdownAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }
    }
}
```

## Quick Start

For products that manage their own configuration:

```csharp
using Lib.GAB;

// Create a host-side GABP server
var server = Gabp.CreateSimpleServer("My Host Product", "1.0.0");

// Register a tool manually
server.Tools.RegisterTool("host/status", _ => Task.FromResult<object>(new
{
    status = "running",
    timestamp = DateTime.UtcNow
}));

// Start the server
await server.StartAsync();
Console.WriteLine($"Server running on port {server.Port}");
Console.WriteLine($"Token: {server.Token}");

// Keep running
Console.ReadKey();
await server.StopAsync();
```

## Attribute-Based Tools

```csharp
using Lib.GAB;
using Lib.GAB.Tools;

public class HostTools
{
    [Tool("data/get", Description = "Get application data", ResultDescription = "The requested identifier and its resolved value.")]
    [ToolResponse("dataId", Type = "string", Description = "Requested data identifier")]
    [ToolResponse("value", Type = "string", Description = "Resolved value")]
    public object GetData([ToolParameter(Description = "Data ID")] string dataId)
    {
        return new { dataId, value = GetDataValue(dataId) };
    }

    [Tool("action/execute", Description = "Execute an action", ResultDescription = "Whether the action succeeded and which action type ran.")]
    [ToolResponse("success", Type = "boolean", Description = "True when the action completed successfully")]
    [ToolResponse("actionType", Type = "string", Description = "Action that was executed")]
    public async Task<object> ExecuteAction(
        [ToolParameter(Description = "Action type")] string actionType,
        [ToolParameter(Description = "Parameters")] string parameters)
    {
        await ExecuteActionAsync(actionType, parameters);
        return new { success = true, actionType };
    }
}

// Register tools from a class instance
var hostTools = new HostTools();
var server = Gabp.CreateServerWithInstance("My Host Product", "1.0.0", hostTools);
await server.StartAsync();
```

### Documenting Tool Responses

Use `ResultDescription` when you want to describe what a successful result means or what useful handle it returns. Lib.GAB emits that text as the root `description` of `outputSchema`.

Use `[ToolResponse]` when individual response fields are worth surfacing in machine-readable metadata. When present, Lib.GAB also includes those fields in `outputSchema`, which makes downstream bridges such as GABS surface richer tool metadata to AI clients.

```csharp
[Tool(
    "screen/capture",
    Description = "Capture the current screen state",
    ResultDescription = "Whether capture succeeded, the high-level UI screen type, and an optional detail message.")]
[ToolResponse("success", Type = "boolean", Description = "Whether capture succeeded")]
[ToolResponse("screenType", Type = "string", Description = "High-level UI screen name")]
[ToolResponse("message", Type = "string", Description = "Optional detail", Always = false, Nullable = true)]
public object CaptureScreen()
{
    return new
    {
        success = true,
        screenType = "main_menu",
        message = (string)null
    };
}
```

For many tools, `ResultDescription` is enough by itself. Field-level response metadata stays optional.

## Event Broadcasting

```csharp
// Register event channels
server.Events.RegisterChannel("host/status_change", "Host status events");
server.Events.RegisterChannel("data/update", "Data update events");

// Emit events
await server.Events.EmitEventAsync("host/status_change", new
{
    status = "ready",
    timestamp = DateTime.UtcNow
});
```

## Optional Attention Support

Enable attention support when your product needs to surface important async host state that the bridge must acknowledge before continuing. When enabled, Lib.GAB adds:

- `attention/current`
- `attention/ack`
- `attention/opened`
- `attention/updated`
- `attention/cleared`

```csharp
using Lib.GAB;
using Lib.GAB.Attention;

var server = Gabp.CreateServer()
    .UseAppInfo("My Host Product", "1.0.0")
    .UseGabsEnvironmentIfAvailable()
    .EnableAttentionSupport()
    .Build();

await server.Attention.PublishAsync(new AttentionItem
{
    AttentionId = "attn_42",
    Severity = "error",
    Blocking = true,
    StateInvalidated = true,
    Summary = "Selection action failed and prior game-state assumptions may no longer be valid.",
    CausalMethod = "rimworld/select_pawn",
    OpenedAtSequence = 1201,
    LatestSequence = 1237,
    DiagnosticsCursor = 1237,
    TotalUrgentEntries = 37
});
```

Use the same `attentionId` when updating an existing item. Acknowledging the matching item through `attention/ack` clears it and emits `attention/cleared`.

Most tool authors do not need direct attention logic. The usual pattern in an embedded bridge product is:

- normal tool code returns ordinary success or failure results
- the integration layer decides which async logs or operation failures should open attention
- only advanced integrations publish attention items directly

## API Reference

### GabpServer

The main server class that handles GABP connections and protocol implementation.

**Properties:**
- `Port`: The port the server is listening on
- `Token`: The authentication token for bridge connections
- `Tools`: Tool registry for managing available tools
- `Events`: Event manager for broadcasting events
- `Attention`: Attention manager for optional blocking/advisory attention state

**Methods:**
- `StartAsync()`: Start the server and begin listening for connections
- `StopAsync()`: Stop the server and close all connections

### Server Builder

```csharp
var server = Gabp.CreateServer()
    .UseAppInfo("My Host Product", "1.0.0")
    .UsePort(51000)
    .UseToken("secret-token")
    .EnableAttentionSupport()
    .Build();
```

### Tool Registration

**Manual Registration:**
```csharp
server.Tools.RegisterTool("tool/name", async (parameters) =>
{
    // Tool implementation
    return result;
});
```

**Attribute-Based Registration:**
```csharp
[Tool("tool/name", Description = "Tool description")]
public async Task<object> MyTool(
    [ToolParameter(Description = "Parameter description")] string param)
{
    // Tool implementation
    return result;
}
```

**Assembly Scanning:**
```csharp
server.Tools.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly());
server.Tools.RegisterToolsFromInstance(new MyToolsClass());
```

### Event Management

```csharp
// Register channels
server.Events.RegisterChannel("channel/name", "Description");

// Emit events
await server.Events.EmitEventAsync("channel/name", eventData);

// Get available channels
var channels = server.Events.GetAvailableChannels();
```

## Protocol Compliance

Lib.GAB implements GABP 1.0 specification including:

- **Message Format**: JSON-RPC-inspired request/response/event messages
- **Transport**: LSP-style framing over TCP connections
- **Authentication**: Token-based authentication between bridge and host
- **Core Methods**: `session/hello`, `tools/list`, `tools/call`, `events/subscribe`, `events/unsubscribe`
- **Optional Attention Methods**: `attention/current` and `attention/ack` when enabled
- **Error Handling**: Standard GABP error codes
- **Capability Negotiation**: Advertising supported protocol methods and event channels

## Example

See [`Lib.GAB.Example/Program.cs`](Lib.GAB.Example/Program.cs) for the minimal working host that this README recommends as the first integration step. It shows:

- a single-file host flow you can copy into an existing product
- GABS-aware startup with standalone fallback
- a minimal tool surface with `hello/ping` and `hello/echo`
- one event channel and a small lifecycle event flow

## Requirements

- **Preferred SDK for development and CI**: .NET 10
- **Runtime compatibility**: any host that supports .NET Standard 2.0, including Unity/Mono-based games such as RimWorld
- Windows, macOS, or Linux

## License

MIT License - see LICENSE file for details.
