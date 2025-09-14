# Lib.GAB

A .NET library implementing the GABP (Game Agent Bridge Protocol) server for AI-application communication.

## Overview

Lib.GAB provides a complete GABP-compliant server implementation that allows applications to expose functionality to AI agents and automation tools. The library handles all the protocol details while providing a simple API for registering tools and emitting events.

## Features

- **GABP 1.0 Compliant**: Full implementation of the GABP specification
- **TCP Transport**: Listens on 127.0.0.1 with flexible port configuration
- **Tool Registration**: Manual and attribute-based tool registration
- **Event System**: Real-time event broadcasting to connected bridges
- **Session Management**: Token-based authentication and capability negotiation
- **Easy Integration**: Simple API for quick setup and customization
- **Wide Compatibility**: Targets .NET Standard 2.0

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
    [Tool("data/get", Description = "Get application data")]
    public object GetData([ToolParameter(Description = "Data ID")] string dataId)
    {
        return new { dataId, value = GetDataValue(dataId) };
    }

    [Tool("action/execute", Description = "Execute an action")]
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
- **Core Methods**: session/hello, tools/list, tools/call, events/subscribe
- **Error Handling**: Standard JSON-RPC error codes
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
