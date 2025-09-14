using Lib.GAB;
using Lib.GAB.Tools;

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

// Example program showing how to use Lib.GAB
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting GABP Server Example...");

        // Create server with tools from a class instance
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
            // Start the server
            await server.StartAsync();
            
            Console.WriteLine($"GABP Server started on port {server.Port}");
            Console.WriteLine($"Authentication token: {server.Token}");
            Console.WriteLine();
            
            // Show available tools
            var tools = server.Tools.GetTools();
            Console.WriteLine($"Available tools ({tools.Count}):");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description ?? "No description"}");
                if (tool.Parameters.Count > 0)
                {
                    Console.WriteLine($"    Parameters: {string.Join(", ", tool.Parameters.Select(p => p.Name))}");
                }
            }
            Console.WriteLine();

            // Show available event channels
            var channels = server.Events.GetAvailableChannels();
            Console.WriteLine($"Available event channels ({channels.Count}):");
            foreach (var channel in channels)
            {
                Console.WriteLine($"  - {channel}");
            }
            Console.WriteLine();

            // Simulate some events
            Console.WriteLine("Simulating events...");
            _ = Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(5000);
                    
                    // Emit a player move event
                    await server.Events.EmitEventAsync("player/move", new
                    {
                        playerId = "player1",
                        position = new
                        {
                            x = random.Next(-100, 100),
                            y = 64,
                            z = random.Next(-100, 100)
                        }
                    });

                    // Emit a game status event
                    await server.Events.EmitEventAsync("game/status", new
                    {
                        status = "running",
                        playerCount = 1,
                        timestamp = DateTimeOffset.UtcNow
                    });
                }
            });

            Console.WriteLine("Server is running. Press any key to stop...");
            Console.ReadKey();
        }
        finally
        {
            await server.StopAsync();
            server.Dispose();
            Console.WriteLine("Server stopped.");
        }
    }
}
