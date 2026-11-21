using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lib.GAB.Protocol;
using Lib.GAB.Server;
using Lib.GAB.Tools;
using Lib.GAB.Transport;
using Xunit;

namespace Lib.GAB.Tests;

public class GabpServerMessageFailureTests
{
    // OnMessageReceived is async void, so an exception that leaves message processing terminates the host process.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplyToAClosedConnectionDoesNotEscapeMessageProcessing(bool reportsConnected)
    {
        using var server = new GabpServer();
        var connection = new ClosedConnection(reportsConnected);
        server.OnConnectionEstablished(null, new ConnectionEstablishedEventArgs(connection));

        // An unauthenticated tools/list makes the server reply with an error, which fails on the closed connection.
        var request = new GabpRequest { Id = "550e8400-e29b-41d4-a716-446655440020", Method = "tools/list" };
        var escaped = await Record.ExceptionAsync(() => server.ProcessMessageAsync(connection, request));

        Assert.Null(escaped);
        Assert.True(connection.SendAttempts >= 1);
    }

    [Fact]
    public async Task ServerKeepsServingAfterAClientLeavesDuringAToolCall()
    {
        var tools = new BlockingTools();
        using var server = Gabp.CreateSimpleServer("Test App", "1.0.0");
        server.Tools.RegisterToolsFromInstance(tools);
        await server.StartAsync();

        try
        {
            using (var leaving = new TcpClient())
            {
                await leaving.ConnectAsync("127.0.0.1", server.Port);
                var stream = leaving.GetStream();
                await GabpServerTransportTests.EstablishAuthenticatedSessionAsync(stream, server.Token);
                await GabpServerTransportTests.SendFrameAsync(stream, new
                {
                    v = "gabp/1", id = "550e8400-e29b-41d4-a716-446655440021", type = "request",
                    method = "tools/call", @params = new { name = "test/block", parameters = new { } }
                });
                await tools.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            // Wait until the server has dropped the session, then let the tool reply into the closed connection.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (server.SessionCount > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            Assert.Equal(0, server.SessionCount);
            tools.Release.SetResult(true);
            await tools.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));

            using var next = new TcpClient();
            await next.ConnectAsync("127.0.0.1", server.Port);
            var nextStream = next.GetStream();
            await GabpServerTransportTests.EstablishAuthenticatedSessionAsync(nextStream, server.Token);
            await GabpServerTransportTests.SendFrameAsync(nextStream, new
            {
                v = "gabp/1", id = "550e8400-e29b-41d4-a716-446655440022", type = "request",
                method = "tools/call", @params = new { name = "test/ping", parameters = new { } }
            });
            using var response = JsonDocument.Parse(await GabpServerTransportTests.ReadFrameAsync(nextStream).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("pong", response.RootElement.GetProperty("result").GetString());
        }
        finally
        {
            await server.StopAsync();
        }
    }

    private sealed class BlockingTools
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        [Tool("test/block", Description = "Wait until the test releases the call")]
        public async Task<string> Block()
        {
            Entered.TrySetResult(true);
            await Release.Task;
            Returned.TrySetResult(true);
            return "released";
        }

        [Tool("test/ping", Description = "Reply with pong")]
        public string Ping() => "pong";
    }

    private sealed class ClosedConnection(bool reportsConnected) : IConnection
    {
        public string Id => "closed-connection";

        public bool IsConnected => reportsConnected;

        public int SendAttempts { get; private set; }

        public event EventHandler Disconnected { add { } remove { } }

        public Task SendMessageAsync(GabpMessage message, CancellationToken cancellationToken = default)
        {
            SendAttempts++;
            throw new InvalidOperationException("Connection is not active");
        }

        public void Dispose()
        {
        }
    }
}
