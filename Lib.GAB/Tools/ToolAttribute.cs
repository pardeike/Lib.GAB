using System;

namespace Lib.GAB.Tools
{
    /// <summary>
    /// Attribute to mark methods as GABP tools
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ToolAttribute : Attribute
    {
        /// <summary>
        /// The name of the tool (e.g., "inventory/get", "world/place_block")
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human-readable description of what the tool does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether this tool requires authentication
        /// </summary>
        public bool RequiresAuth { get; set; } = true;

        public ToolAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tool name cannot be null or empty", nameof(name));
            
            Name = name;
        }
    }

    /// <summary>
    /// Attribute to mark parameters of tool methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class ToolParameterAttribute : Attribute
    {
        /// <summary>
        /// Human-readable description of the parameter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether this parameter is required
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// Default value for the parameter if not provided
        /// </summary>
        public object DefaultValue { get; set; }
    }
}