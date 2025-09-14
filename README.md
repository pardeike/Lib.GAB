# Lib.GAB

A .NET library implementing the GABP (Game Agent Bridge Protocol) server for AI-game communication.

## Overview

Lib.GAB provides a complete GABP-compliant server implementation that allows games and applications to expose functionality to AI agents and automation tools. The library handles all the protocol details while providing a simple API for registering tools and emitting events.

## Features

- **GABP 1.0 Compliant**: Full implementation of the GABP specification
- **TCP Transport**: Listens on 127.0.0.1 with flexible port configuration
- **Tool Registration**: Manual and attribute-based tool registration
- **Event System**: Real-time event broadcasting to connected bridges
- **Session Management**: Token-based authentication and capability negotiation
- **Easy Integration**: Simple API for quick setup and customization
- **Wide Compatibility**: Targets .NET Standard 2.0 for maximum compatibility including .NET Framework 4.7.2

## Quick Start

### Basic Usage

```csharp
using Lib.GAB;

// Create a simple server
var server = Gabp.CreateSimpleServer("My Game", "1.0.0");

// Register a tool manually
server.Tools.RegisterTool("game/status", _ => Task.FromResult<object>(new
{
    status = "running",
    players = 1
}));

// Start the server
await server.StartAsync();
Console.WriteLine($"Server running on port {server.Port}");
Console.WriteLine($"Token: {server.Token}");

// Keep running
Console.ReadKey();
await server.StopAsync();
```

### Using Attribute-Based Tools

```csharp
using Lib.GAB;
using Lib.GAB.Tools;

public class GameTools
{
    [Tool("inventory/get", Description = "Get player inventory")]
    public object GetInventory([ToolParameter(Description = "Player ID")] string playerId)
    {
        return new { playerId, items = GetPlayerItems(playerId) };
    }

    [Tool("world/place_block", Description = "Place a block in the world")]
    public async Task<object> PlaceBlock(
        [ToolParameter(Description = "Block type")] string blockType,
        [ToolParameter(Description = "X coordinate")] int x,
        [ToolParameter(Description = "Y coordinate")] int y,
        [ToolParameter(Description = "Z coordinate")] int z)
    {
        await PlaceBlockAsync(blockType, x, y, z);
        return new { success = true, position = new { x, y, z } };
    }
}

// Register tools from a class instance
var gameTools = new GameTools();
var server = Gabp.CreateServerWithInstance("My Game", "1.0.0", gameTools);
await server.StartAsync();
```

### Event Broadcasting

```csharp
// Register event channels
server.Events.RegisterChannel("player/move", "Player movement events");
server.Events.RegisterChannel("world/block_change", "Block change events");

// Emit events
await server.Events.EmitEventAsync("player/move", new
{
    playerId = "player1",
    position = new { x = 100, y = 64, z = 200 }
});
```

## API Reference

### GabpServer

The main server class that handles GABP connections and protocol implementation.

#### Properties
- `Port`: The port the server is listening on
- `Token`: The authentication token for bridge connections
- `Tools`: Tool registry for managing available tools
- `Events`: Event manager for broadcasting events

#### Methods
- `StartAsync()`: Start the server and begin listening for connections
- `StopAsync()`: Stop the server and close all connections

### Tool Registration

#### Manual Registration
```csharp
server.Tools.RegisterTool("tool/name", async (parameters) =>
{
    // Tool implementation
    return result;
});
```

#### Attribute-Based Registration
```csharp
[Tool("tool/name", Description = "Tool description")]
public async Task<object> MyTool(
    [ToolParameter(Description = "Parameter description")] string param)
{
    // Tool implementation
    return result;
}
```

#### Assembly Scanning
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

### Server Configuration

```csharp
var server = Gabp.CreateServer()
    .UsePort(12345)                    // Specific port (0 for auto)
    .UseToken("my-custom-token")       // Custom auth token
    .UseAgentId("my-game-mod")         // Agent identifier
    .UseAppInfo("My Game", "2.0.0")    // App information
    .WriteConfigFile(true)             // Write config for bridges
    .Build();
```

## Protocol Compliance

Lib.GAB implements GABP 1.0 specification including:

- **Message Format**: JSON-RPC-inspired request/response/event messages
- **Transport**: LSP-style framing over TCP connections
- **Authentication**: Token-based authentication with config file
- **Core Methods**: session/hello, tools/list, tools/call, events/subscribe
- **Error Handling**: Standard JSON-RPC error codes
- **Capability Negotiation**: Advertising available tools and events

## Bridge Connection

When a server starts, it creates a configuration file that bridges can use to connect:

**Windows**: `%APPDATA%\gabp\bridge.json`  
**macOS**: `~/Library/Application Support/gabp/bridge.json`  
**Linux**: `~/.config/gabp/bridge.json`

The configuration contains the connection details and authentication token.

## Example

See `Lib.GAB.Example` for a complete working example showing:
- Tool registration with attributes
- Event channel setup
- Event broadcasting
- Server lifecycle management

## Requirements

- **.NET Standard 2.0** compatible runtime:
  - .NET Framework 4.7.2 or later (for RimWorld mods, Unity applications)
  - .NET Core 2.0 or later
  - .NET 5.0 or later
- Windows, macOS, or Linux

## RimWorld Mod Compatibility

This library is specifically designed to work with **RimWorld mods** running on **.NET Framework 4.7.2** and Unity. The library targets .NET Standard 2.0 to ensure maximum compatibility with Unity and older .NET Framework versions.

## License

MIT License - see LICENSE file for details.
