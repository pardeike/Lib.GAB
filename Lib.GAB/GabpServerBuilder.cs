using System.Reflection;
using Lib.GAB.Protocol;
using Lib.GAB.Server;

namespace Lib.GAB;

/// <summary>
/// Builder for creating and configuring GABP servers
/// </summary>
public class GabpServerBuilder
{
    private readonly GabpServerConfig _config = new();

    /// <summary>
    /// Set the port to listen on (0 for automatic port selection)
    /// </summary>
    public GabpServerBuilder UsePort(int port)
    {
        _config.Port = port;
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
    /// Whether to write configuration file for bridges to connect
    /// </summary>
    public GabpServerBuilder WriteConfigFile(bool write = true)
    {
        _config.WriteConfigFile = write;
        return this;
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
}