using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lib.GAB.Protocol
{
    /// <summary>
    /// Parameters for session/hello request
    /// </summary>
    public class SessionHelloParams
    {
        /// <summary>
        /// Authentication token
        /// </summary>
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Bridge software version
        /// </summary>
        [JsonProperty("bridgeVersion")]
        public string BridgeVersion { get; set; } = string.Empty;

        /// <summary>
        /// Operating system ("windows", "macos", or "linux")
        /// </summary>
        [JsonProperty("platform")]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for this session
        /// </summary>
        [JsonProperty("launchId")]
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
        [JsonProperty("agentId")]
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// Application information
        /// </summary>
        [JsonProperty("app")]
        public AppInfo App { get; set; } = new AppInfo();

        /// <summary>
        /// Supported features
        /// </summary>
        [JsonProperty("capabilities")]
        public Capabilities Capabilities { get; set; } = new Capabilities();

        /// <summary>
        /// GABP schema version
        /// </summary>
        [JsonProperty("schemaVersion")]
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
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Game or application version
        /// </summary>
        [JsonProperty("version")]
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
        [JsonProperty("tools")]
        public List<string> Tools { get; set; } = new List<string>();

        /// <summary>
        /// Available event channels
        /// </summary>
        [JsonProperty("events")]
        public List<string> Events { get; set; } = new List<string>();

        /// <summary>
        /// Available resources
        /// </summary>
        [JsonProperty("resources")]
        public List<string> Resources { get; set; } = new List<string>();
    }
}