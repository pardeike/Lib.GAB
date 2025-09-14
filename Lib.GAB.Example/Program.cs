using System;
using System.Linq;
using System.Threading.Tasks;
using Lib.GAB;
using Lib.GAB.Tools;
using Lib.GAB.Server;

// Example game/application tools
public class GameTools
{
    [Tool("inventory/get", Description = "Get player inventory")]
    public object GetInventory([ToolParameter(Description = "Player ID")] string playerId = "player1")
    {
        return new
        {
            playerId,
            items = new[]
            {
                new { name = "sword", count = 1 },
                new { name = "potion", count = 5 },
                new { name = "gold", count = 100 }
            }
        };
    }

    [Tool("world/place_block", Description = "Place a block in the world")]
    public object PlaceBlock(
        [ToolParameter(Description = "Block type")] string blockType,
        [ToolParameter(Description = "X coordinate")] int x,
        [ToolParameter(Description = "Y coordinate")] int y,
        [ToolParameter(Description = "Z coordinate")] int z)
    {
        Console.WriteLine($"Placing {blockType} at ({x}, {y}, {z})");
        return new { success = true, position = new { x, y, z }, blockType };
    }

    [Tool("player/teleport", Description = "Teleport player to coordinates")]
    public async Task<object> TeleportPlayer(
        [ToolParameter(Description = "Player ID")] string playerId,
        [ToolParameter(Description = "X coordinate")] double x,
        [ToolParameter(Description = "Y coordinate")] double y,
        [ToolParameter(Description = "Z coordinate")] double z)
    {
        // Simulate async operation
        await Task.Delay(100);
        
        Console.WriteLine($"Teleporting {playerId} to ({x}, {y}, {z})");
        return new { playerId, position = new { x, y, z }, success = true };
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting GABP Server Example...");

        // Check if we're running under GABS
        if (Gabp.IsRunningUnderGabs())
        {
            Console.WriteLine("\nRunning under GABS - using GABS-aware configuration");
            await RunGabsAwareExample();
        }
        else
        {
            Console.WriteLine("\nNot running under GABS - demonstrating traditional and external config modes");
            
            // Example 1: Traditional server creation (no config file written by default now)
            Console.WriteLine("\nExample 1: Traditional server (no external config)");
            await RunTraditionalExample();

            Console.WriteLine("\nPress Enter to continue to Example 2...");
            Console.ReadLine();

            // Example 2: Server with external configuration (simulating GABS bridge usage)
            Console.WriteLine("\nExample 2: Server with external configuration");
            await RunExternalConfigExample();
        }
    }

    static async Task RunGabsAwareExample()
    {
        // Create server with automatic GABS environment detection
        var gameTools = new GameTools();
        var server = Gabp.CreateGabsAwareServerWithInstance("Example Game", "1.0.0", gameTools);

        // Register some event channels
        server.Events.RegisterChannel("player/move", "Player movement events");
        server.Events.RegisterChannel("world/block_change", "World block change events");
        server.Events.RegisterChannel("game/status", "Game status events");

        // Register a custom tool manually
        server.Tools.RegisterTool("game/status", _ => Task.FromResult<object?>(new
        {
            status = "running",
            players = 1,
            uptime = TimeSpan.FromMinutes(5).ToString()
        }));

        try
        {
            await server.StartAsync();
            
            Console.WriteLine($"GABS-aware GABP Server started on port {server.Port}");
            Console.WriteLine($"Authentication token: {server.Token}");
            Console.WriteLine("NOTE: Configuration automatically detected from GABS environment variables.");
            
            ShowServerInfo(server);

            Console.WriteLine("Server is running. Press any key to stop...");
            Console.ReadKey();
        }
        finally
        {
            await server.StopAsync();
            server.Dispose();
            Console.WriteLine("GABS-aware server stopped.");
        }
    }

    static async Task RunTraditionalExample()
    {
        // Create server with tools from a class instance (traditional way)
        var gameTools = new GameTools();
        var server = Gabp.CreateServerWithInstance("Example Game", "1.0.0", gameTools, port: 0);

        // Register some event channels
        server.Events.RegisterChannel("player/move", "Player movement events");
        server.Events.RegisterChannel("world/block_change", "World block change events");
        server.Events.RegisterChannel("game/status", "Game status events");

        // Register a custom tool manually
        server.Tools.RegisterTool("game/status", _ => Task.FromResult<object?>(new
        {
            status = "running",
            players = 1,
            uptime = TimeSpan.FromMinutes(5).ToString()
        }));

        try
        {
            await server.StartAsync();
            
            Console.WriteLine($"Traditional GABP Server started on port {server.Port}");
            Console.WriteLine($"Authentication token: {server.Token}");
            Console.WriteLine("NOTE: No bridge config file is written by default now.");
            
            ShowServerInfo(server);

            Console.WriteLine("Server is running. Press any key to stop...");
            Console.ReadKey();
        }
        finally
        {
            await server.StopAsync();
            server.Dispose();
            Console.WriteLine("Traditional server stopped.");
        }
    }

    static async Task RunExternalConfigExample()
    {
        // Simulate reading configuration from GABS bridge file or environment variables
        var externalPort = 12345;
        var externalToken = "external-token-from-gabs";
        var gameId = "example-game-mod";

        Console.WriteLine($"Simulating external config: port={externalPort}, token={externalToken}, gameId={gameId}");

        // Create server with external configuration
        var gameTools = new GameTools();
        var server = Gabp.CreateServerWithInstanceAndExternalConfig(
            "Example Game", "1.0.0", gameTools, externalPort, externalToken, gameId);

        // Register event channels
        server.Events.RegisterChannel("player/move", "Player movement events");
        server.Events.RegisterChannel("world/block_change", "World block change events");
        server.Events.RegisterChannel("game/status", "Game status events");

        // Register a custom tool manually
        server.Tools.RegisterTool("game/status", _ => Task.FromResult<object?>(new
        {
            status = "running",
            players = 1,
            uptime = TimeSpan.FromMinutes(5).ToString()
        }));

        try
        {
            await server.StartAsync();
            
            Console.WriteLine($"External Config GABP Server started on port {server.Port}");
            Console.WriteLine($"Authentication token: {server.Token}");
            Console.WriteLine("NOTE: Using external configuration from GABS bridge.");
            
            ShowServerInfo(server);

            Console.WriteLine("Server is running. Press any key to stop...");
            Console.ReadKey();
        }
        finally
        {
            await server.StopAsync();
            server.Dispose();
            Console.WriteLine("External config server stopped.");
        }
    }

    static void ShowServerInfo(GabpServer server)
    {
        // Show available tools
        var tools = server.Tools.GetTools();
        Console.WriteLine($"\nAvailable tools ({tools.Count}):");
        foreach (var tool in tools)
        {
            Console.WriteLine($"  - {tool.Name}: {tool.Description ?? "No description"}");
            if (tool.Parameters.Count > 0)
            {
                Console.WriteLine($"    Parameters: {string.Join(", ", tool.Parameters.Select(p => p.Name))}");
            }
        }

        // Show available event channels
        var channels = server.Events.GetAvailableChannels();
        Console.WriteLine($"\nAvailable event channels ({channels.Count}):");
        foreach (var channel in channels)
        {
            Console.WriteLine($"  - {channel}");
        }
        Console.WriteLine();
    }
}
