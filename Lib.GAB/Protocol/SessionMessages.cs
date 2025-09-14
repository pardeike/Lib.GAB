using System.Text.Json.Serialization;

namespace Lib.GAB.Protocol;

/// <summary>
/// Parameters for session/hello request
/// </summary>
public class SessionHelloParams
{
    /// <summary>
    /// Authentication token
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Bridge software version
    /// </summary>
    [JsonPropertyName("bridgeVersion")]
    public string BridgeVersion { get; set; } = string.Empty;

    /// <summary>
    /// Operating system ("windows", "macos", or "linux")
    /// </summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this session
    /// </summary>
    [JsonPropertyName("launchId")]
    public string LaunchId { get; set; } = string.Empty;
}

/// <summary>
/// Result for session/welcome response
/// </summary>
public class SessionWelcomeResult
{
    /// <summary>
    /// Unique identifier for the mod instance
    /// </summary>
    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Application information
    /// </summary>
    [JsonPropertyName("app")]
    public AppInfo App { get; set; } = new();

    /// <summary>
    /// Supported features
    /// </summary>
    [JsonPropertyName("capabilities")]
    public Capabilities Capabilities { get; set; } = new();

    /// <summary>
    /// GABP schema version
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";
}

/// <summary>
/// Application information
/// </summary>
public class AppInfo
{
    /// <summary>
    /// Game or application name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Game or application version
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Capabilities supported by the server
/// </summary>
public class Capabilities
{
    /// <summary>
    /// Available tools/methods
    /// </summary>
    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    /// <summary>
    /// Available event channels
    /// </summary>
    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = new();

    /// <summary>
    /// Available resources
    /// </summary>
    [JsonPropertyName("resources")]
    public List<string> Resources { get; set; } = new();
}