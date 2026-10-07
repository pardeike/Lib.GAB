using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Lib.GAB.Tools
{
    /// <summary>
    /// Ambient information about the attribute-bound tool call that is currently executing.
    /// </summary>
    /// <remarks>
    /// The context is available to tool methods registered through
    /// <see cref="IToolRegistry.RegisterToolsFromInstance"/> or
    /// <see cref="IToolRegistry.RegisterToolsFromAssembly"/> for the duration of the method call,
    /// including continuations of async methods. It flows with the logical call context
    /// (<see cref="AsyncLocal{T}"/>), so work handed to another thread through a custom queue
    /// does not see it; read it at the start of the tool method in that case.
    /// Outside a tool call, and for handlers registered through <see cref="IToolRegistry.RegisterTool"/>,
    /// <see cref="Current"/> is <c>null</c>.
    /// </remarks>
    public sealed class ToolCallContext
    {
        private static readonly AsyncLocal<ToolCallContext> CurrentContext = new AsyncLocal<ToolCallContext>();

        internal ToolCallContext(
            string toolName,
            IDictionary<string, object> arguments,
            IList<string> parameterNames,
            IList<string> unrecognizedArguments)
        {
            ToolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
            Arguments = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(arguments ?? new Dictionary<string, object>()));
            ParameterNames = new ReadOnlyCollection<string>(new List<string>(parameterNames ?? new string[0]));
            UnrecognizedArguments = new ReadOnlyCollection<string>(new List<string>(unrecognizedArguments ?? new string[0]));
        }

        /// <summary>
        /// The context of the tool call executing on the current logical call flow, or <c>null</c>.
        /// </summary>
        public static ToolCallContext Current => CurrentContext.Value;

        /// <summary>
        /// Registered name of the tool being called, for example <c>math/add</c>.
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// Argument keys and values as supplied by the caller, before binding to method parameters.
        /// Values are the JSON-deserialized representations (strings, numbers, booleans, or JSON tokens).
        /// </summary>
        public IReadOnlyDictionary<string, object> Arguments { get; }

        /// <summary>
        /// Names of the parameters declared by the tool method, in declaration order.
        /// </summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>
        /// Supplied argument keys that matched no tool parameter, neither exactly nor case-insensitively.
        /// These arguments were ignored during binding.
        /// </summary>
        public IReadOnlyList<string> UnrecognizedArguments { get; }

        /// <summary>
        /// True when the caller supplied at least one argument that matched no tool parameter.
        /// </summary>
        public bool HasUnrecognizedArguments => UnrecognizedArguments.Count > 0;

        internal static ToolCallContext Enter(ToolCallContext context)
        {
            var previous = CurrentContext.Value;
            CurrentContext.Value = context;
            return previous;
        }

        internal static void Restore(ToolCallContext previous)
        {
            CurrentContext.Value = previous;
        }
    }
}
