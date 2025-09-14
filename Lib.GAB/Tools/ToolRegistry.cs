using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Lib.GAB.Tools;

/// <summary>
/// Default implementation of the tool registry
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredTool> _tools = new();

    private class RegisteredTool
    {
        public ToolInfo Info { get; set; } = new();
        public Func<object?, Task<object?>> Handler { get; set; } = _ => Task.FromResult<object?>(null);
    }

    public void RegisterTool(string name, Func<object?, Task<object?>> handler, ToolInfo? info = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(name));
        
        ArgumentNullException.ThrowIfNull(handler);

        var toolInfo = info ?? new ToolInfo { Name = name };
        toolInfo.Name = name; // Ensure name matches

        _tools[name] = new RegisteredTool
        {
            Info = toolInfo,
            Handler = handler
        };
    }

    public void RegisterToolsFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface) continue;

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                var toolAttribute = method.GetCustomAttribute<ToolAttribute>();
                if (toolAttribute == null) continue;

                RegisterMethodAsTool(null, method, toolAttribute);
            }
        }
    }

    public void RegisterToolsFromInstance(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            var toolAttribute = method.GetCustomAttribute<ToolAttribute>();
            if (toolAttribute == null) continue;

            RegisterMethodAsTool(instance, method, toolAttribute);
        }
    }

    private void RegisterMethodAsTool(object? instance, MethodInfo method, ToolAttribute attribute)
    {
        var toolInfo = new ToolInfo
        {
            Name = attribute.Name,
            Description = attribute.Description,
            RequiresAuth = attribute.RequiresAuth
        };

        // Build parameter info
        var parameters = method.GetParameters();
        foreach (var param in parameters)
        {
            var paramAttribute = param.GetCustomAttribute<ToolParameterAttribute>();
            var paramInfo = new ToolParameterInfo
            {
                Name = param.Name ?? "param",
                Type = param.ParameterType,
                Description = paramAttribute?.Description,
                Required = paramAttribute?.Required ?? !param.HasDefaultValue,
                DefaultValue = param.HasDefaultValue ? param.DefaultValue : paramAttribute?.DefaultValue
            };
            toolInfo.Parameters.Add(paramInfo);
        }

        // Create handler function
        async Task<object?> Handler(object? args)
        {
            try
            {
                var paramValues = PrepareParameters(method, args);
                var result = method.Invoke(instance, paramValues);

                // Handle async methods
                if (result is Task task)
                {
                    await task;
                    
                    // Get result from Task<T>
                    if (task.GetType().IsGenericType)
                    {
                        var property = task.GetType().GetProperty("Result");
                        return property?.GetValue(task);
                    }
                    
                    return null;
                }

                return result;
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the actual exception
                throw ex.InnerException ?? ex;
            }
        }

        RegisterTool(attribute.Name, Handler, toolInfo);
    }

    private static object?[] PrepareParameters(MethodInfo method, object? args)
    {
        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];

        if (args == null)
        {
            // Use default values
            for (int i = 0; i < parameters.Length; i++)
            {
                values[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
            }
            return values;
        }

        // Convert args to dictionary for parameter matching
        Dictionary<string, object?> argDict;
        
        if (args is JsonElement jsonElement)
        {
            argDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonElement.GetRawText()) 
                     ?? new Dictionary<string, object?>();
        }
        else if (args is Dictionary<string, object?> dict)
        {
            argDict = dict;
        }
        else
        {
            // Try to convert object to dictionary using JSON serialization
            var json = JsonSerializer.Serialize(args);
            argDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) 
                     ?? new Dictionary<string, object?>();
        }

        // Map arguments to parameters
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? $"param{i}";

            if (argDict.TryGetValue(paramName, out var value))
            {
                values[i] = ConvertParameter(value, param.ParameterType);
            }
            else
            {
                values[i] = param.HasDefaultValue ? param.DefaultValue : null;
            }
        }

        return values;
    }

    private static object? ConvertParameter(object? value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsAssignableFrom(value.GetType())) return value;

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        // Handle primitive type conversions
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // Fall back to JSON serialization/deserialization
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize(json, targetType);
        }
    }

    public bool UnregisterTool(string name)
    {
        return _tools.TryRemove(name, out _);
    }

    public IReadOnlyList<ToolInfo> GetTools()
    {
        return _tools.Values.Select(t => t.Info).ToList();
    }

    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }

    public async Task<object?> CallToolAsync(string name, object? parameters = null)
    {
        if (!_tools.TryGetValue(name, out var tool))
            throw new InvalidOperationException($"Tool '{name}' is not registered");

        return await tool.Handler(parameters);
    }
}