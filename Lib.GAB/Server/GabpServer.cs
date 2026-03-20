using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lib.GAB.Attention;
using Lib.GAB.Events;
using Lib.GAB.Protocol;
using Lib.GAB.Tools;
using Lib.GAB.Transport;
using GabpRuntime = Gabp.Runtime;

namespace Lib.GAB.Server
{
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
        public AppInfo AppInfo { get; set; } = new AppInfo { Name = "GABP Server", Version = "1.0.0" };

        /// <summary>
        /// Whether additive attention support should be exposed.
        /// </summary>
        public bool EnableAttentionSupport { get; set; }
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
        private readonly IAttentionManager _attentionManager;
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new ConcurrentDictionary<string, SessionInfo>();
        private bool _disposed;

        private static readonly IReadOnlyList<string> BaseProtocolMethods = new[]
        {
            GabpRuntime.GabpProtocol.SessionHelloMethod,
            GabpRuntime.GabpProtocol.ToolsListMethod,
            GabpRuntime.GabpProtocol.ToolsCallMethod,
            "events/subscribe",
            "events/unsubscribe"
        };

        private class SessionInfo
        {
            public IConnection Connection { get; set; }
            public bool IsAuthenticated { get; set; }
            public string BridgeVersion { get; set; }
            public string Platform { get; set; }
            public string LaunchId { get; set; }
        }

        public GabpServer(GabpServerConfig config = null)
        {
            _config = config ?? new GabpServerConfig();
            _transport = new TcpTransport(_config.Port);
            _toolRegistry = new ToolRegistry();
            _eventManager = new EventManager();
            _attentionManager = new AttentionManager(_eventManager, _config.EnableAttentionSupport);

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
        /// Attention manager for publishing and acknowledging important game state.
        /// </summary>
        public IAttentionManager Attention => _attentionManager;

        /// <summary>
        /// Start the server
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _transport.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _transport.StopAsync(cancellationToken);
            DisposeActiveSessions();
        }

        private void SetupTransportEvents()
        {
            _transport.ConnectionEstablished += OnConnectionEstablished;
            _transport.MessageReceived += OnMessageReceived;
        }

        private void OnConnectionEstablished(object sender, ConnectionEstablishedEventArgs e)
        {
            _sessions[e.Connection.Id] = new SessionInfo
            {
                Connection = e.Connection,
                IsAuthenticated = false
            };

            e.Connection.Disconnected += (_, __) => {
                SessionInfo removedSession;
                _sessions.TryRemove(e.Connection.Id, out removedSession);
            };
        }

        private async void OnMessageReceived(object sender, MessageReceivedEventArgs e)
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
            SessionInfo session;
            if (!_sessions.TryGetValue(connection.Id, out session))
                return;

            switch (message)
            {
                case GabpRequest request:
                    await HandleRequestAsync(connection, session, request);
                    break;
                
                // Responses and events are typically not handled by the server
                case GabpResponse _:
                case GabpEvent _:
                    break;
            }
        }

        private async Task HandleRequestAsync(IConnection connection, SessionInfo session, GabpRequest request)
        {
            // Handle session methods without authentication
            if (request.Method == GabpRuntime.GabpProtocol.SessionHelloMethod)
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
                case GabpRuntime.GabpProtocol.ToolsListMethod:
                    await HandleToolsListAsync(connection, request);
                    break;
                
                case GabpRuntime.GabpProtocol.ToolsCallMethod:
                    await HandleToolsCallAsync(connection, request);
                    break;
                
                case "events/subscribe":
                    await HandleEventsSubscribeAsync(connection, request);
                    break;
                
                case "events/unsubscribe":
                    await HandleEventsUnsubscribeAsync(connection, request);
                    break;

                case AttentionProtocol.CurrentMethod when _attentionManager.IsEnabled:
                    await HandleAttentionCurrentAsync(connection, request);
                    break;

                case AttentionProtocol.AckMethod when _attentionManager.IsEnabled:
                    await HandleAttentionAckAsync(connection, request);
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
                var helloParams = GabpRuntime.GabpJson.Deserialize<GabpRuntime.SessionHelloParams>(
                    JsonConvert.SerializeObject(request.Params));

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
                var welcomeResult = new GabpRuntime.SessionWelcomeResult
                {
                    AgentId = _config.AgentId,
                    App = new GabpRuntime.GabpAppInfo
                    {
                        Name = _config.AppInfo.Name,
                        Version = _config.AppInfo.Version
                    },
                    Capabilities = new GabpRuntime.GabpCapabilities
                    {
                        Methods = GetSupportedProtocolMethods(),
                        Events = _eventManager.GetAvailableChannels(),
                        Resources = new List<string>() // TODO: Add resource support
                    },
                    SchemaVersion = GabpRuntime.RuntimeMetadata.TargetGabpSchemaVersion
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
                title = string.IsNullOrWhiteSpace(t.Title) ? BuildToolTitle(t.Name) : t.Title,
                description = t.Description,
                inputSchema = BuildInputSchema(t.Parameters),
                parameters = t.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type.Name,
                    description = p.Description,
                    required = p.Required,
                    defaultValue = p.DefaultValue
                }).ToList(),
                outputSchema = BuildOutputSchema(t.ResultDescription, t.ResponseFields),
                requiresAuth = t.RequiresAuth
            }).ToList();

            await SendResponseAsync(connection, request.Id, new { tools });
        }

        private static Dictionary<string, object> BuildInputSchema(List<ToolParameterInfo> parameters)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var parameter in parameters)
            {
                var property = new Dictionary<string, object>
                {
                    ["type"] = MapTypeToJsonSchemaType(parameter.Type)
                };

                if (!string.IsNullOrWhiteSpace(parameter.Description))
                {
                    property["description"] = parameter.Description;
                }

                if (parameter.DefaultValue != null)
                {
                    property["default"] = parameter.DefaultValue;
                }

                properties[parameter.Name] = property;

                if (parameter.Required)
                {
                    required.Add(parameter.Name);
                }
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            };

            if (required.Count > 0)
            {
                schema["required"] = required;
            }

            return schema;
        }

        private static Dictionary<string, object> BuildOutputSchema(string resultDescription, List<ToolResponseFieldInfo> fields)
        {
            if (fields.Count == 0 && string.IsNullOrWhiteSpace(resultDescription))
            {
                return new Dictionary<string, object>();
            }

            var schema = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(resultDescription))
            {
                schema["description"] = resultDescription;
            }

            if (fields.Count == 0)
            {
                return schema;
            }

            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var f in fields)
            {
                var prop = new Dictionary<string, object>
                {
                    ["type"] = f.Type
                };

                if (f.Nullable)
                    prop["nullable"] = true;

                if (!string.IsNullOrEmpty(f.Description))
                    prop["description"] = f.Description;

                properties[f.Name] = prop;

                if (f.Always)
                    required.Add(f.Name);
            }

            schema["type"] = "object";
            schema["properties"] = properties;

            if (required.Count > 0)
                schema["required"] = required;

            return schema;
        }

        private static string BuildToolTitle(string toolName)
        {
            var parts = toolName
                .Split(new[] { '/', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part))
                .ToList();

            return parts.Count == 0 ? toolName : string.Join(" ", parts);
        }

        private static string MapTypeToJsonSchemaType(Type type)
        {
            var targetType = Nullable.GetUnderlyingType(type) ?? type;

            if (targetType == typeof(string) || targetType == typeof(char) || targetType == typeof(Guid))
            {
                return "string";
            }

            if (targetType == typeof(bool))
            {
                return "boolean";
            }

            if (targetType == typeof(byte) ||
                targetType == typeof(sbyte) ||
                targetType == typeof(short) ||
                targetType == typeof(ushort) ||
                targetType == typeof(int) ||
                targetType == typeof(uint) ||
                targetType == typeof(long) ||
                targetType == typeof(ulong))
            {
                return "integer";
            }

            if (targetType == typeof(float) ||
                targetType == typeof(double) ||
                targetType == typeof(decimal))
            {
                return "number";
            }

            if (targetType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType))
            {
                return "array";
            }

            return "object";
        }

        private async Task HandleToolsCallAsync(IConnection connection, GabpRequest request)
        {
            try
            {
                var paramsJson = JsonConvert.SerializeObject(request.Params);
                var callParams = GabpRuntime.GabpJson.Deserialize<GabpRuntime.ToolsCallParams>(paramsJson);

                if (callParams == null || string.IsNullOrWhiteSpace(callParams.Name))
                {
                    await SendErrorResponseAsync(connection, request.Id, 
                        GabpErrorCodes.InvalidParams, "Missing 'name' parameter");
                    return;
                }

                var toolName = callParams.Name;
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

                object arguments = null;
                var paramsObject = JObject.Parse(paramsJson);

                // Respect the GABS compatibility path from PR #10:
                // prefer "parameters" when present, then fall back to canonical "arguments".
                if (paramsObject.TryGetValue("parameters", out var parametersToken) &&
                    parametersToken.Type != JTokenType.Null)
                {
                    arguments = parametersToken;
                }
                else if (callParams.Arguments != null)
                {
                    arguments = callParams.Arguments;
                }

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
                var subscribeParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(request.Params));

                if (subscribeParams == null || !subscribeParams.ContainsKey("channels"))
                {
                    await SendErrorResponseAsync(connection, request.Id, 
                        GabpErrorCodes.InvalidParams, "Missing 'channels' parameter");
                    return;
                }

                var channelsObj = subscribeParams["channels"];
                var channels = JsonConvert.DeserializeObject<List<string>>(
                    JsonConvert.SerializeObject(channelsObj)) ?? new List<string>();

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
                var unsubscribeParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(request.Params));

                if (unsubscribeParams == null || !unsubscribeParams.ContainsKey("channels"))
                {
                    await SendErrorResponseAsync(connection, request.Id, 
                        GabpErrorCodes.InvalidParams, "Missing 'channels' parameter");
                    return;
                }

                var channelsObj = unsubscribeParams["channels"];
                var channels = JsonConvert.DeserializeObject<List<string>>(
                    JsonConvert.SerializeObject(channelsObj)) ?? new List<string>();

                var unsubscribed = await _eventManager.UnsubscribeAsync(connection, channels);
                
                await SendResponseAsync(connection, request.Id, new { unsubscribed });
            }
            catch (Exception ex)
            {
                await SendErrorResponseAsync(connection, request.Id, 
                    GabpErrorCodes.InternalError, $"Unsubscription failed: {ex.Message}");
            }
        }

        private async Task HandleAttentionCurrentAsync(IConnection connection, GabpRequest request)
        {
            var currentAttention = _attentionManager.GetCurrent();
            await SendResponseAsync(connection, request.Id, new AttentionCurrentResult
            {
                Attention = currentAttention
            });
        }

        private async Task HandleAttentionAckAsync(IConnection connection, GabpRequest request)
        {
            try
            {
                var ackParams = JsonConvert.DeserializeObject<AttentionAckParams>(
                    JsonConvert.SerializeObject(request.Params));

                if (ackParams == null || string.IsNullOrWhiteSpace(ackParams.AttentionId))
                {
                    await SendErrorResponseAsync(connection, request.Id,
                        GabpErrorCodes.InvalidParams, "Missing 'attentionId' parameter");
                    return;
                }

                var result = await _attentionManager.AcknowledgeAsync(ackParams.AttentionId);
                await SendResponseAsync(connection, request.Id, result);
            }
            catch (Exception ex)
            {
                await SendErrorResponseAsync(connection, request.Id,
                    GabpErrorCodes.InternalError, $"Attention acknowledgement failed: {ex.Message}");
            }
        }

        private async Task SendResponseAsync(IConnection connection, string requestId, object result)
        {
            var response = new GabpResponse
            {
                Id = requestId,
                Result = result
            };

            await connection.SendMessageAsync(response);
        }

        private async Task SendErrorResponseAsync(IConnection connection, string requestId, int errorCode, string message, object data = null)
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

            if (_attentionManager.IsEnabled)
            {
                _eventManager.RegisterChannel(AttentionProtocol.OpenedChannel, "Attention lifecycle events when a blocking or advisory item opens");
                _eventManager.RegisterChannel(AttentionProtocol.UpdatedChannel, "Attention lifecycle events when the current item is updated");
                _eventManager.RegisterChannel(AttentionProtocol.ClearedChannel, "Attention lifecycle events when the current item is cleared");
            }
        }

        private List<string> GetSupportedProtocolMethods()
        {
            var methods = new List<string>(BaseProtocolMethods);

            if (_attentionManager.IsEnabled)
            {
                methods.Add(AttentionProtocol.CurrentMethod);
                methods.Add(AttentionProtocol.AckMethod);
            }

            return methods;
        }

        private void DisposeActiveSessions()
        {
            foreach (var session in _sessions.Values.ToList())
            {
                try
                {
                    session.Connection?.Dispose();
                }
                catch
                {
                    // Best-effort shutdown. Remaining cleanup continues through disconnection handlers.
                }
            }

            _sessions.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _transport.ConnectionEstablished -= OnConnectionEstablished;
            _transport.MessageReceived -= OnMessageReceived;

            try
            {
                _transport.StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Continue disposing active sessions and transport even if stopping fails.
            }

            DisposeActiveSessions();
            _transport?.Dispose();
        }
    }
}
