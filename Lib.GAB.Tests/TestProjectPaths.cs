using System;
using System.IO;

namespace Lib.GAB.Tests;

internal static class TestProjectPaths
{
    public static string CurrentTargetFrameworkMoniker { get; } = GetCurrentTargetFrameworkMoniker();

    public static string CurrentTargetFrameworkFolder { get; } = CurrentTargetFrameworkMoniker;

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ExampleBinDirectory { get; } = Path.Combine(RepoRoot, "Lib.GAB.Example", "bin");

    public static string GetExampleAssemblyPath()
    {
        var releasePath = Path.Combine(ExampleBinDirectory, "Release", CurrentTargetFrameworkFolder, "Lib.GAB.Example.dll");
        var debugPath = Path.Combine(ExampleBinDirectory, "Debug", CurrentTargetFrameworkFolder, "Lib.GAB.Example.dll");

        if (File.Exists(releasePath))
        {
            return releasePath;
        }

        if (File.Exists(debugPath))
        {
            return debugPath;
        }

        return releasePath;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Lib.GAB.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing Lib.GAB.sln.");
    }

    private static string GetCurrentTargetFrameworkMoniker()
    {
        var frameworkName = AppContext.TargetFrameworkName;
        const string versionPrefix = "Version=v";

        if (string.IsNullOrWhiteSpace(frameworkName))
        {
            return "net10.0";
        }

        var versionStart = frameworkName.IndexOf(versionPrefix, StringComparison.Ordinal);
        if (versionStart < 0)
        {
            return "net10.0";
        }

        var version = frameworkName.Substring(versionStart + versionPrefix.Length);
        return $"net{version}";
    }
}
