using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using Lib.GAB.Events;
using Lib.GAB.Protocol;
using Lib.GAB.Tools;
using Lib.GAB.Transport;

namespace Lib.GAB.Server;

/// <summary>
/// Configuration for GABP server
/// </summary>
public class GabpServerConfig
{
    /// <summary>
    /// Port to listen on (0 for automatic port selection)
    /// </summary>
    public int Port { get; set; } = 0;

    /// <summary>
    /// Authentication token required for connections
    /// </summary>
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Agent ID to identify this server instance
    /// </summary>
    public string AgentId { get; set; } = "gabp-server";

    /// <summary>
    /// Application information
    /// </summary>
    public AppInfo AppInfo { get; set; } = new() { Name = "GABP Server", Version = "1.0.0" };

    /// <summary>
    /// Whether to write configuration file for bridges to connect
    /// </summary>
    public bool WriteConfigFile { get; set; } = true;
}

/// <summary>
/// Main GABP server implementation
/// </summary>
public class GabpServer : IDisposable
{
    private readonly GabpServerConfig _config;
    private readonly ITransport _transport;
    private readonly IToolRegistry _toolRegistry;
    private readonly IEventManager _eventManager;
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private bool _disposed;

    private class SessionInfo
    {
        public IConnection Connection { get; set; } = null!;
        public bool IsAuthenticated { get; set; }
        public string? BridgeVersion { get; set; }
        public string? Platform { get; set; }
        public string? LaunchId { get; set; }
    }

    public GabpServer(GabpServerConfig? config = null)
    {
        _config = config ?? new GabpServerConfig();
        _transport = new TcpTransport(_config.Port);
        _toolRegistry = new ToolRegistry();
        _eventManager = new EventManager();

        SetupTransportEvents();
        RegisterCoreTools();
        RegisterCoreEventChannels();
    }

    /// <summary>
    /// Get the port the server is listening on
    /// </summary>
    public int Port => _transport is TcpTransport tcpTransport ? tcpTransport.Port : 0;

    /// <summary>
    /// Get the authentication token
    /// </summary>
    public string Token => _config.Token;

    /// <summary>
    /// Tool registry for registering custom tools
    /// </summary>
    public IToolRegistry Tools => _toolRegistry;

    /// <summary>
    /// Event manager for emitting events
    /// </summary>
    public IEventManager Events => _eventManager;

    /// <summary>
    /// Start the server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _transport.StartAsync(cancellationToken);
        
        if (_config.WriteConfigFile)
        {
            await WriteConfigFileAsync();
        }
    }

    /// <summary>
    /// Stop the server
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _transport.StopAsync(cancellationToken);
    }

    private void SetupTransportEvents()
    {
        _transport.ConnectionEstablished += OnConnectionEstablished;
        _transport.MessageReceived += OnMessageReceived;
    }

    private void OnConnectionEstablished(object? sender, ConnectionEstablishedEventArgs e)
    {
        _sessions[e.Connection.Id] = new SessionInfo
        {
            Connection = e.Connection,
            IsAuthenticated = false
        };

        e.Connection.Disconnected += (_, _) => _sessions.TryRemove(e.Connection.Id, out _);
    }

    private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            await HandleMessageAsync(e.Connection, e.Message);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(e.Connection, e.Message.Id, 
                GabpErrorCodes.InternalError, $"Internal server error: {ex.Message}");
        }
    }

    private async Task HandleMessageAsync(IConnection connection, GabpMessage message)
    {
        if (!_sessions.TryGetValue(connection.Id, out var session))
            return;

        switch (message)
        {
            case GabpRequest request:
                await HandleRequestAsync(connection, session, request);
                break;
            
            // Responses and events are typically not handled by the server
            case GabpResponse:
            case GabpEvent:
                break;
        }
    }

    private async Task HandleRequestAsync(IConnection connection, SessionInfo session, GabpRequest request)
    {
        // Handle session methods without authentication
        if (request.Method == "session/hello")
        {
            await HandleSessionHelloAsync(connection, session, request);
            return;
        }

        // All other methods require authentication
        if (!session.IsAuthenticated)
        {
            await SendErrorResponseAsync(connection, request.Id, 
                GabpErrorCodes.SessionNotEstablished, "Session not established. Send session/hello first.");
            return;
        }

        // Handle core methods
        switch (request.Method)
        {
            case "tools/list":
                await HandleToolsListAsync(connection, request);
                break;
            
            case "tools/call":
                await HandleToolsCallAsync(connection, request);
                break;
            
            case "events/subscribe":
                await HandleEventsSubscribeAsync(connection, request);
                break;
            
            case "events/unsubscribe":
                await HandleEventsUnsubscribeAsync(connection, request);
                break;
            
            default:
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.MethodNotFound, $"Method '{request.Method}' not found");
                break;
        }
    }

    private async Task HandleSessionHelloAsync(IConnection connection, SessionInfo session, GabpRequest request)
    {
        try
        {
            var helloParams = JsonSerializer.Deserialize<SessionHelloParams>(
                JsonSerializer.Serialize(request.Params));

            if (helloParams == null || helloParams.Token != _config.Token)
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.AuthenticationFailed, "Invalid authentication token");
                return;
            }

            // Update session info
            session.IsAuthenticated = true;
            session.BridgeVersion = helloParams.BridgeVersion;
            session.Platform = helloParams.Platform;
            session.LaunchId = helloParams.LaunchId;

            // Send welcome response
            var welcomeResult = new SessionWelcomeResult
            {
                AgentId = _config.AgentId,
                App = _config.AppInfo,
                Capabilities = new Capabilities
                {
                    Tools = _toolRegistry.GetTools().Select(t => t.Name).ToList(),
                    Events = _eventManager.GetAvailableChannels(),
                    Resources = new List<string>() // TODO: Add resource support
                },
                SchemaVersion = "1.0"
            };

            await SendResponseAsync(connection, request.Id, welcomeResult);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(connection, request.Id, 
                GabpErrorCodes.InvalidParams, $"Invalid session/hello parameters: {ex.Message}");
        }
    }

    private async Task HandleToolsListAsync(IConnection connection, GabpRequest request)
    {
        var tools = _toolRegistry.GetTools().Select(t => new
        {
            name = t.Name,
            description = t.Description,
            parameters = t.Parameters.Select(p => new
            {
                name = p.Name,
                type = p.Type.Name,
                description = p.Description,
                required = p.Required,
                defaultValue = p.DefaultValue
            }).ToList(),
            requiresAuth = t.RequiresAuth
        }).ToList();

        await SendResponseAsync(connection, request.Id, new { tools });
    }

    private async Task HandleToolsCallAsync(IConnection connection, GabpRequest request)
    {
        try
        {
            var callParams = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                JsonSerializer.Serialize(request.Params));

            if (callParams == null || !callParams.TryGetValue("name", out var toolNameObj))
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.InvalidParams, "Missing 'name' parameter");
                return;
            }

            var toolName = toolNameObj?.ToString();
            if (string.IsNullOrEmpty(toolName))
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.InvalidParams, "Tool name cannot be empty");
                return;
            }

            if (!_toolRegistry.HasTool(toolName))
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.ToolNotFound, $"Tool '{toolName}' not found");
                return;
            }

            callParams.TryGetValue("arguments", out var arguments);
            var result = await _toolRegistry.CallToolAsync(toolName, arguments);
            
            await SendResponseAsync(connection, request.Id, result);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(connection, request.Id, 
                GabpErrorCodes.InternalError, $"Tool execution failed: {ex.Message}");
        }
    }

    private async Task HandleEventsSubscribeAsync(IConnection connection, GabpRequest request)
    {
        try
        {
            var subscribeParams = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                JsonSerializer.Serialize(request.Params));

            if (subscribeParams == null || !subscribeParams.TryGetValue("channels", out var channelsObj))
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.InvalidParams, "Missing 'channels' parameter");
                return;
            }

            var channels = JsonSerializer.Deserialize<List<string>>(
                JsonSerializer.Serialize(channelsObj)) ?? new List<string>();

            var subscribed = await _eventManager.SubscribeAsync(connection, channels);
            
            await SendResponseAsync(connection, request.Id, new { subscribed });
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(connection, request.Id, 
                GabpErrorCodes.InternalError, $"Subscription failed: {ex.Message}");
        }
    }

    private async Task HandleEventsUnsubscribeAsync(IConnection connection, GabpRequest request)
    {
        try
        {
            var unsubscribeParams = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                JsonSerializer.Serialize(request.Params));

            if (unsubscribeParams == null || !unsubscribeParams.TryGetValue("channels", out var channelsObj))
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.InvalidParams, "Missing 'channels' parameter");
                return;
            }

            var channels = JsonSerializer.Deserialize<List<string>>(
                JsonSerializer.Serialize(channelsObj)) ?? new List<string>();

            var unsubscribed = await _eventManager.UnsubscribeAsync(connection, channels);
            
            await SendResponseAsync(connection, request.Id, new { unsubscribed });
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(connection, request.Id, 
                GabpErrorCodes.InternalError, $"Unsubscription failed: {ex.Message}");
        }
    }

    private async Task SendResponseAsync(IConnection connection, string requestId, object? result)
    {
        var response = new GabpResponse
        {
            Id = requestId,
            Result = result
        };

        await connection.SendMessageAsync(response);
    }

    private async Task SendErrorResponseAsync(IConnection connection, string requestId, int errorCode, string message, object? data = null)
    {
        var response = new GabpResponse
        {
            Id = requestId,
            Error = new GabpError
            {
                Code = errorCode,
                Message = message,
                Data = data
            }
        };

        await connection.SendMessageAsync(response);
    }

    private void RegisterCoreTools()
    {
        // Core tools are handled directly in HandleRequestAsync
        // but we still register them for discovery
    }

    private void RegisterCoreEventChannels()
    {
        _eventManager.RegisterChannel("system/status", "System status events");
        _eventManager.RegisterChannel("system/log", "System log events");
    }

    private async Task WriteConfigFileAsync()
    {
        var configDir = GetConfigDirectory();
        Directory.CreateDirectory(configDir);
        
        var configPath = Path.Combine(configDir, "bridge.json");
        
        var config = new
        {
            token = _config.Token,
            transport = new
            {
                type = "tcp",
                address = Port.ToString()
            },
            metadata = new
            {
                pid = Environment.ProcessId,
                startTime = DateTimeOffset.UtcNow.ToString("O"),
                launchId = Guid.NewGuid().ToString()
            }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(configPath, json);
    }

    private static string GetConfigDirectory()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gabp"),
            PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "gabp"),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "gabp")
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _transport?.Dispose();
    }
}