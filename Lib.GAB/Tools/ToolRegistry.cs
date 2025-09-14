using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Lib.GAB.Tools
{
    /// <summary>
    /// Default implementation of the tool registry
    /// </summary>
    public class ToolRegistry : IToolRegistry
    {
        private readonly ConcurrentDictionary<string, RegisteredTool> _tools = new ConcurrentDictionary<string, RegisteredTool>();

        private class RegisteredTool
        {
            public ToolInfo Info { get; set; } = new ToolInfo();
            public Func<object, Task<object>> Handler { get; set; } = _ => Task.FromResult<object>(null);
        }

        public void RegisterTool(string name, Func<object, Task<object>> handler, ToolInfo info = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tool name cannot be null or empty", nameof(name));
            
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

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
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                RegisterToolsFromType(type, null);
            }
        }

        public void RegisterToolsFromInstance(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            RegisterToolsFromType(instance.GetType(), instance);
        }

        private void RegisterToolsFromType(Type type, object instance)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<ToolAttribute>();
                if (toolAttr == null) continue;

                // Skip static methods if we have an instance, or instance methods if we don't
                if ((instance == null && !method.IsStatic) || (instance != null && method.IsStatic))
                    continue;

                var toolInfo = new ToolInfo
                {
                    Name = toolAttr.Name,
                    Description = toolAttr.Description,
                    RequiresAuth = toolAttr.RequiresAuth,
                    Parameters = GetParameterInfo(method)
                };

                RegisterTool(toolAttr.Name, CreateHandler(method, instance), toolInfo);
            }
        }

        private List<ToolParameterInfo> GetParameterInfo(MethodInfo method)
        {
            var parameters = new List<ToolParameterInfo>();
            
            foreach (var param in method.GetParameters())
            {
                var paramAttr = param.GetCustomAttribute<ToolParameterAttribute>();
                
                parameters.Add(new ToolParameterInfo
                {
                    Name = param.Name,
                    Type = param.ParameterType,
                    Description = paramAttr?.Description,
                    Required = paramAttr?.Required ?? !param.HasDefaultValue,
                    DefaultValue = param.HasDefaultValue ? param.DefaultValue : null
                });
            }
            
            return parameters;
        }

        private Func<object, Task<object>> CreateHandler(MethodInfo method, object instance)
        {
            return async (parameters) =>
            {
                try
                {
                    var paramValues = ConvertParameters(method, parameters);
                    var result = method.Invoke(instance, paramValues);
                    
                    if (result is Task task)
                    {
                        await task;
                        
                        // Check if it's Task<T>
                        if (task.GetType().IsGenericType)
                        {
                            var prop = task.GetType().GetProperty("Result");
                            return prop?.GetValue(task);
                        }
                        return null;
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error calling tool '{method.Name}': {ex.Message}", ex);
                }
            };
        }

        private object[] ConvertParameters(MethodInfo method, object parameters)
        {
            var methodParams = method.GetParameters();
            var paramValues = new object[methodParams.Length];
            
            if (parameters == null)
            {
                // Use default values
                for (int i = 0; i < methodParams.Length; i++)
                {
                    paramValues[i] = methodParams[i].HasDefaultValue ? methodParams[i].DefaultValue : null;
                }
                return paramValues;
            }

            // Convert parameters from JSON object
            var paramDict = new Dictionary<string, object>();
            if (parameters is string jsonString)
            {
                paramDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString) ?? new Dictionary<string, object>();
            }
            else
            {
                var json = JsonConvert.SerializeObject(parameters);
                paramDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                
                if (paramDict.ContainsKey(param.Name))
                {
                    try
                    {
                        var value = paramDict[param.Name];
                        paramValues[i] = ConvertValue(value, param.ParameterType);
                    }
                    catch
                    {
                        paramValues[i] = param.HasDefaultValue ? param.DefaultValue : null;
                    }
                }
                else
                {
                    paramValues[i] = param.HasDefaultValue ? param.DefaultValue : null;
                }
            }
            
            return paramValues;
        }

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;
            
            // Handle basic type conversions
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                // Try JSON deserialization as fallback
                var json = JsonConvert.SerializeObject(value);
                return JsonConvert.DeserializeObject(json, targetType);
            }
        }

        public bool UnregisterTool(string name)
        {
            RegisteredTool removedTool;
            return _tools.TryRemove(name, out removedTool);
        }

        public IList<ToolInfo> GetTools()
        {
            return _tools.Values.Select(t => t.Info).ToList();
        }

        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }

        public async Task<object> CallToolAsync(string name, object parameters = null)
        {
            RegisteredTool tool;
            if (!_tools.TryGetValue(name, out tool))
                throw new ArgumentException($"Tool '{name}' not found", nameof(name));

            return await tool.Handler(parameters);
        }
    }
}