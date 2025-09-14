using System;
using System.Reflection;
using Lib.GAB.Protocol;
using Lib.GAB.Server;

namespace Lib.GAB
{
    /// <summary>
    /// Builder for creating and configuring GABP servers
    /// </summary>
    public class GabpServerBuilder
    {
        private readonly GabpServerConfig _config = new GabpServerConfig();

        /// <summary>
        /// Set the port to listen on (0 for automatic port selection)
        /// </summary>
        public GabpServerBuilder UsePort(int port)
        {
            _config.Port = port;
            return this;
        }

        /// <summary>
        /// Set the port to listen on if it hasn't been set already (used for fallback configuration)
        /// </summary>
        public GabpServerBuilder UsePortIfNotSet(int port)
        {
            if (_config.Port == 0)
            {
                _config.Port = port;
            }
            return this;
        }

        /// <summary>
        /// Set the authentication token
        /// </summary>
        public GabpServerBuilder UseToken(string token)
        {
            _config.Token = token;
            return this;
        }

        /// <summary>
        /// Set the agent ID
        /// </summary>
        public GabpServerBuilder UseAgentId(string agentId)
        {
            _config.AgentId = agentId;
            return this;
        }

        /// <summary>
        /// Set application information
        /// </summary>
        public GabpServerBuilder UseAppInfo(string name, string version)
        {
            _config.AppInfo = new AppInfo { Name = name, Version = version };
            return this;
        }



        /// <summary>
        /// Set the port and authentication token from external configuration (e.g., from GABS bridge file)
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <param name="token">The authentication token</param>
        /// <returns>The builder instance for method chaining</returns>
        public GabpServerBuilder UseExternalConfig(int port, string token)
        {
            _config.Port = port;
            _config.Token = token;
            return this;
        }

        /// <summary>
        /// Set the port, authentication token, and game ID from external configuration
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <param name="token">The authentication token</param>
        /// <param name="gameId">The game ID to use as agent ID</param>
        /// <returns>The builder instance for method chaining</returns>
        public GabpServerBuilder UseExternalConfig(int port, string token, string gameId)
        {
            _config.Port = port;
            _config.Token = token;
            _config.AgentId = gameId;
            return this;
        }

        /// <summary>
        /// Automatically configure from GABS environment variables if present
        /// </summary>
        /// <returns>The builder instance for method chaining, or null if GABS environment variables are not found</returns>
        public GabpServerBuilder UseGabsEnvironmentIfAvailable()
        {
            var gabsConfig = TryReadGabsEnvironment();
            if (gabsConfig != null)
            {
                _config.Port = gabsConfig.Port;
                _config.Token = gabsConfig.Token;
                if (!string.IsNullOrEmpty(gabsConfig.GameId))
                {
                    _config.AgentId = gabsConfig.GameId;
                }
            }
            return this;
        }

        /// <summary>
        /// Try to read GABS environment variables
        /// </summary>
        /// <returns>GABS configuration if available, null otherwise</returns>
        private static GabsEnvironmentConfig TryReadGabsEnvironment()
        {
            var gameId = Environment.GetEnvironmentVariable("GABS_GAME_ID");
            var portStr = Environment.GetEnvironmentVariable("GABP_SERVER_PORT");
            var token = Environment.GetEnvironmentVariable("GABP_TOKEN");

            if (!string.IsNullOrEmpty(portStr) && !string.IsNullOrEmpty(token) &&
                int.TryParse(portStr, out int port))
            {
                return new GabsEnvironmentConfig
                {
                    Port = port,
                    Token = token,
                    GameId = gameId
                };
            }

            return null;
        }

        /// <summary>
        /// Internal class for GABS environment configuration
        /// </summary>
        private class GabsEnvironmentConfig
        {
            public int Port { get; set; }
            public string Token { get; set; }
            public string GameId { get; set; }
        }

        /// <summary>
        /// Build the GABP server
        /// </summary>
        public GabpServer Build()
        {
            return new GabpServer(_config);
        }
    }

    /// <summary>
    /// Main entry point for Lib.GAB functionality
    /// </summary>
    public static class Gabp
    {
        /// <summary>
        /// Create a new GABP server builder
        /// </summary>
        public static GabpServerBuilder CreateServer()
        {
            return new GabpServerBuilder();
        }

        /// <summary>
        /// Create a simple GABP server with default configuration
        /// </summary>
        public static GabpServer CreateSimpleServer(string appName, string appVersion, int port = 0)
        {
            return CreateServer()
                .UseAppInfo(appName, appVersion)
                .UsePort(port)
                .Build();
        }

        /// <summary>
        /// Create a GABP server and register tools from an assembly
        /// </summary>
        public static GabpServer CreateServerWithAssembly(string appName, string AppVersion, Assembly assembly, int port = 0)
        {
            var server = CreateSimpleServer(appName, AppVersion, port);
            server.Tools.RegisterToolsFromAssembly(assembly);
            return server;
        }

        /// <summary>
        /// Create a GABP server and register tools from an object instance
        /// </summary>
        public static GabpServer CreateServerWithInstance(string appName, string appVersion, object instance, int port = 0)
        {
            var server = CreateSimpleServer(appName, appVersion, port);
            server.Tools.RegisterToolsFromInstance(instance);
            return server;
        }

        /// <summary>
        /// Create a GABP server with external configuration and register tools from an object instance
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="appVersion">Application version</param>
        /// <param name="instance">Object instance containing tools marked with [Tool] attributes</param>
        /// <param name="port">Port from external configuration</param>
        /// <param name="token">Token from external configuration</param>
        /// <returns>Configured GABP server with registered tools</returns>
        public static GabpServer CreateServerWithInstanceAndExternalConfig(string appName, string appVersion, object instance, int port, string token)
        {
            var server = CreateServerWithExternalConfig(appName, appVersion, port, token);
            server.Tools.RegisterToolsFromInstance(instance);
            return server;
        }

        /// <summary>
        /// Create a GABP server with external configuration and register tools from an object instance
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="appVersion">Application version</param>
        /// <param name="instance">Object instance containing tools marked with [Tool] attributes</param>
        /// <param name="port">Port from external configuration</param>
        /// <param name="token">Token from external configuration</param>
        /// <param name="gameId">Game ID from external configuration</param>
        /// <returns>Configured GABP server with registered tools</returns>
        public static GabpServer CreateServerWithInstanceAndExternalConfig(string appName, string appVersion, object instance, int port, string token, string gameId)
        {
            var server = CreateServerWithExternalConfig(appName, appVersion, port, token, gameId);
            server.Tools.RegisterToolsFromInstance(instance);
            return server;
        }

        /// <summary>
        /// Create a GABP server with external configuration (port and token from GABS bridge)
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="appVersion">Application version</param>
        /// <param name="port">Port from external configuration</param>
        /// <param name="token">Token from external configuration</param>
        /// <returns>Configured GABP server</returns>
        public static GabpServer CreateServerWithExternalConfig(string appName, string appVersion, int port, string token)
        {
            return CreateServer()
                .UseAppInfo(appName, appVersion)
                .UseExternalConfig(port, token)
                .Build();
        }

        /// <summary>
        /// Create a GABP server with external configuration (port, token, and game ID from GABS bridge)
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="appVersion">Application version</param>
        /// <param name="port">Port from external configuration</param>
        /// <param name="token">Token from external configuration</param>
        /// <param name="gameId">Game ID from external configuration</param>
        /// <returns>Configured GABP server</returns>
        public static GabpServer CreateServerWithExternalConfig(string appName, string appVersion, int port, string token, string gameId)
        {
            return CreateServer()
                .UseAppInfo(appName, appVersion)
                .UseExternalConfig(port, token, gameId)
                .Build();
        }

        /// <summary>
        /// Create a GABP server that automatically detects GABS environment variables
        /// Falls back to standard configuration if GABS environment is not detected
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="appVersion">Application version</param>
        /// <param name="fallbackPort">Port to use if GABS environment is not detected (0 for automatic)</param>
        /// <returns>Configured GABP server</returns>
        public static GabpServer CreateGabsAwareServer(string appName, string appVersion, int fallbackPort = 0)
        {
            return CreateServer()
                .UseAppInfo(appName, appVersion)
                .UseGabsEnvironmentIfAvailable()
                .UsePortIfNotSet(fallbackPort) // Only used if GABS environment wasn't detected
                .Build();
        }

        /// <summary>
        /// Create a GABP server that automatically detects GABS environment variables and registers tools from an instance
        /// Falls back to standard configuration if GABS environment is not detected
        /// </summary>
        /// <param name="appName">Application name</param>
        /// <param name="appVersion">Application version</param>
        /// <param name="instance">Object instance containing tools marked with [Tool] attributes</param>
        /// <param name="fallbackPort">Port to use if GABS environment is not detected (0 for automatic)</param>
        /// <returns>Configured GABP server with registered tools</returns>
        public static GabpServer CreateGabsAwareServerWithInstance(string appName, string appVersion, object instance, int fallbackPort = 0)
        {
            var server = CreateGabsAwareServer(appName, appVersion, fallbackPort);
            server.Tools.RegisterToolsFromInstance(instance);
            return server;
        }

        /// <summary>
        /// Check if the current environment has GABS configuration
        /// </summary>
        /// <returns>True if GABS environment variables are detected</returns>
        public static bool IsRunningUnderGabs()
        {
            var gameId = Environment.GetEnvironmentVariable("GABS_GAME_ID");
            var portStr = Environment.GetEnvironmentVariable("GABP_SERVER_PORT");
            var token = Environment.GetEnvironmentVariable("GABP_TOKEN");

            return !string.IsNullOrEmpty(portStr) && !string.IsNullOrEmpty(token) &&
                   int.TryParse(portStr, out _);
        }
    }
}