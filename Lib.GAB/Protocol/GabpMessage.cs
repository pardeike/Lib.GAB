using System.Text.Json.Serialization;

namespace Lib.GAB.Protocol;

/// <summary>
/// Base class for all GABP messages following the envelope structure
/// </summary>
public abstract class GabpMessage
{
    /// <summary>
    /// Protocol version identifier. Must be "gabp/1" for version 1.x
    /// </summary>
    [JsonPropertyName("v")]
    public string Version { get; set; } = "gabp/1";

    /// <summary>
    /// Unique identifier for the message, formatted as a UUID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Message type: request, response, or event
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Request message with method and optional parameters
/// </summary>
public class GabpRequest : GabpMessage
{
    public override string Type => "request";

    /// <summary>
    /// The method name being invoked
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the method call
    /// </summary>
    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

/// <summary>
/// Response message with result or error
/// </summary>
public class GabpResponse : GabpMessage
{
    public override string Type => "response";

    /// <summary>
    /// The successful result of the method call
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    /// <summary>
    /// Error information if the method call failed
    /// </summary>
    [JsonPropertyName("error")]
    public GabpError? Error { get; set; }
}

/// <summary>
/// Event message with channel, sequence, and payload
/// </summary>
public class GabpEvent : GabpMessage
{
    public override string Type => "event";

    /// <summary>
    /// Event channel name
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Sequence number for the event (≥ 0)
    /// </summary>
    [JsonPropertyName("seq")]
    public int Sequence { get; set; }

    /// <summary>
    /// Event data
    /// </summary>
    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    /// <summary>
    /// Optional ISO 8601 timestamp when event occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }
}

/// <summary>
/// Error object for response messages
/// </summary>
public class GabpError
{
    /// <summary>
    /// Numeric error code
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Human-readable error description
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error-specific data
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}