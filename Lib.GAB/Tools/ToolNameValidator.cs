using System;
using System.Text.RegularExpressions;

namespace Lib.GAB.Tools
{
    public static class ToolNameValidator
    {
        public const string CanonicalPattern = "^[a-z][a-z0-9_-]*(/[a-z][a-z0-9_-]*)+$";

        private static readonly Regex CanonicalToolNameRegex = new Regex(
            CanonicalPattern,
            RegexOptions.CultureInvariant);

        public static bool IsValid(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && CanonicalToolNameRegex.IsMatch(name);
        }

        public static void EnsureValid(string name, string parameterName = "name")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tool name cannot be null or empty", parameterName);

            if (!IsValid(name))
                throw new ArgumentException(
                    $"Tool name '{name}' must be a canonical GABP tool name matching {CanonicalPattern} (for example 'inventory/get'). Dotted MCP adapter names such as 'inventory.get' are not valid GABP tool names.",
                    parameterName);
        }
    }
}
