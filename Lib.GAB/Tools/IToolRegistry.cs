using System.Reflection;

namespace Lib.GAB.Tools;

/// <summary>
/// Information about a registered tool
/// </summary>
public class ToolInfo
{
    /// <summary>
    /// Tool name (e.g., "inventory/get")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parameter information
    /// </summary>
    public List<ToolParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Whether authentication is required
    /// </summary>
    public bool RequiresAuth { get; set; } = true;
}

/// <summary>
/// Information about a tool parameter
/// </summary>
public class ToolParameterInfo
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type
    /// </summary>
    public Type Type { get; set; } = typeof(object);

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the parameter is required
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Default value if not provided
    /// </summary>
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Registry for managing GABP tools
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Register a tool method manually
    /// </summary>
    void RegisterTool(string name, Func<object?, Task<object?>> handler, ToolInfo? info = null);

    /// <summary>
    /// Register all tools from an assembly by scanning for ToolAttribute
    /// </summary>
    void RegisterToolsFromAssembly(Assembly assembly);

    /// <summary>
    /// Register all tools from an object instance by scanning for ToolAttribute
    /// </summary>
    void RegisterToolsFromInstance(object instance);

    /// <summary>
    /// Unregister a tool
    /// </summary>
    bool UnregisterTool(string name);

    /// <summary>
    /// Get information about all registered tools
    /// </summary>
    IReadOnlyList<ToolInfo> GetTools();

    /// <summary>
    /// Check if a tool is registered
    /// </summary>
    bool HasTool(string name);

    /// <summary>
    /// Call a registered tool
    /// </summary>
    Task<object?> CallToolAsync(string name, object? parameters = null);
}