namespace Lib.GAB.Protocol;

/// <summary>
/// Standard GABP error codes following JSON-RPC specification
/// </summary>
public static class GabpErrorCodes
{
    /// <summary>
    /// Invalid Request - The JSON sent is not a valid request object
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// Method Not Found - The method does not exist or is not available
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// Invalid Params - Invalid method parameter(s)
    /// </summary>
    public const int InvalidParams = -32602;

    /// <summary>
    /// Internal Error - Internal JSON-RPC error
    /// </summary>
    public const int InternalError = -32603;

    /// <summary>
    /// Server Error - Reserved for implementation-defined server errors (range -32099 to -32000)
    /// </summary>
    public const int ServerError = -32000;

    /// <summary>
    /// Authentication Failed - Invalid token provided
    /// </summary>
    public const int AuthenticationFailed = -31000;

    /// <summary>
    /// Session Not Established - Attempted to call method before successful handshake
    /// </summary>
    public const int SessionNotEstablished = -31001;

    /// <summary>
    /// Tool Not Found - Requested tool does not exist
    /// </summary>
    public const int ToolNotFound = -31002;

    /// <summary>
    /// Event Channel Not Found - Requested event channel does not exist
    /// </summary>
    public const int EventChannelNotFound = -31003;

    /// <summary>
    /// Resource Not Found - Requested resource does not exist
    /// </summary>
    public const int ResourceNotFound = -31004;

    /// <summary>
    /// Method Not Allowed - Method is not allowed in current session state
    /// </summary>
    public const int MethodNotAllowed = -31005;
}