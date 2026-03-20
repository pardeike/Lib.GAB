# Lib.GAB

A .NET library implementing the GABP (Game Agent Bridge Protocol) server for AI-application communication.

## Overview

Lib.GAB provides the higher-level server ergonomics for hosting a GABP endpoint inside a game or application. It builds on the shared `Gabp.Runtime` wire-model package for canonical protocol constants and message contracts, while keeping the integration surface simple: register tools, register event channels, and start the server.

## Installation

```bash
dotnet add package Lib.GAB
```

`Lib.GAB` brings in `Gabp.Runtime` transitively, so consumers do not need a separate direct package reference to the runtime package.

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
- **Wide Compatibility**: Targets .NET Standard 2.0

## GABS Integration

Lib.GAB seamlessly integrates with [GABS](https://github.com/pardeike/GABS) (Game Agent Bridge Server) for AI-controlled gaming experiences.

### Automatic GABS Detection

When your game is launched by GABS, Lib.GAB automatically detects the GABS environment and configures itself appropriately:

```csharp
// Automatically detects and uses GABS configuration if available
// Falls back to standard configuration if not running under GABS
var server = Gabp.CreateGabsAwareServer("My Game", "1.0.0");

// With tools from a class instance
var gameTools = new GameTools();
var server = Gabp.CreateGabsAwareServerWithInstance("My Game", "1.0.0", gameTools);

await server.StartAsync();
```

### GABS Environment Variables

GABS provides configuration through environment variables:
- `GABS_GAME_ID`: Game identifier from GABS configuration
- `GABP_SERVER_PORT`: Port your mod should listen on as GABP server
- `GABP_TOKEN`: Authentication token for GABS connections

### Checking GABS Environment

You can check if your application is running under GABS:

```csharp
if (Gabp.IsRunningUnderGabs())
{
    Console.WriteLine("Running under GABS control");
    // Use GABS-aware server creation
    var server = Gabp.CreateGabsAwareServer("My Game", "1.0.0");
}
else
{
    Console.WriteLine("Running standalone");
    // Use traditional server creation
    var server = Gabp.CreateSimpleServer("My Game", "1.0.0");
}
```

### Manual GABS Configuration

For advanced scenarios, you can manually configure GABS settings:

```csharp
// Read GABS environment manually
var gameId = Environment.GetEnvironmentVariable("GABS_GAME_ID");
var port = int.Parse(Environment.GetEnvironmentVariable("GABP_SERVER_PORT"));
var token = Environment.GetEnvironmentVariable("GABP_TOKEN");

var server = Gabp.CreateServerWithExternalConfig("My Game", "1.0.0", port, token, gameId);
```

### Practical Usage Example

Here's how to use Lib.GAB in a game mod that should work both standalone and with GABS:

```csharp
using System;
using System.Threading.Tasks;
using Lib.GAB;
using Lib.GAB.Tools;

public class GameMod
{
    private GabpServer _server;

    public async Task InitializeAsync()
    {
        // Create server that automatically adapts to the environment
        _server = Gabp.CreateGabsAwareServerWithInstance("My Game Mod", "1.0.0", this);
        
        // Register event channels
        _server.Events.RegisterChannel("player/move", "Player movement events");
        _server.Events.RegisterChannel("game/status", "Game status updates");
        
        await _server.StartAsync();
        
        if (Gabp.IsRunningUnderGabs())
        {
            Console.WriteLine($"Game mod connected to GABS on port {_server.Port}");
        }
        else
        {
            Console.WriteLine($"Game mod running standalone on port {_server.Port}");
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
        // Your game-specific teleport logic here
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

For applications that manage their own configuration:

```csharp
using Lib.GAB;

// Create a simple server
var server = Gabp.CreateSimpleServer("My Application", "1.0.0");

// Register a tool manually
server.Tools.RegisterTool("app/status", _ => Task.FromResult<object>(new
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

public class ApplicationTools
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
var appTools = new ApplicationTools();
var server = Gabp.CreateServerWithInstance("My Application", "1.0.0", appTools);
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
server.Events.RegisterChannel("app/status_change", "Application status events");
server.Events.RegisterChannel("data/update", "Data update events");

// Emit events
await server.Events.EmitEventAsync("app/status_change", new
{
    status = "ready",
    timestamp = DateTime.UtcNow
});
```

## Optional Attention Support

Enable attention support when your integration needs to surface important async game state that the bridge must acknowledge before continuing. When enabled, Lib.GAB adds:

- `attention/current`
- `attention/ack`
- `attention/opened`
- `attention/updated`
- `attention/cleared`

```csharp
using Lib.GAB;
using Lib.GAB.Attention;

var server = Gabp.CreateServer()
    .UseAppInfo("My Game", "1.0.0")
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

Most tool authors do not need direct attention logic. The usual pattern is:

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

**Methods:**
- `StartAsync()`: Start the server and begin listening for connections
- `StopAsync()`: Stop the server and close all connections

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
- **Authentication**: Token-based authentication with config file
- **Core Methods**: `session/hello`, `tools/list`, `tools/call`, `events/subscribe`, `events/unsubscribe`
- **Error Handling**: Standard GABP error codes
- **Capability Negotiation**: Advertising available tools and events

## Example

See `Lib.GAB.Example` for a complete working example showing:
- Tool registration with attributes
- Event channel setup
- Event broadcasting
- Server lifecycle management

## Requirements

- **.NET Standard 2.0** compatible runtime:
  - .NET Framework 4.7.2 or later
  - .NET Core 2.0 or later
  - .NET 5.0 or later
- Windows, macOS, or Linux

## License

MIT License - see LICENSE file for details.
