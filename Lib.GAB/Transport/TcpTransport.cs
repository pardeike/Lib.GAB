using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Lib.GAB.Protocol;

namespace Lib.GAB.Transport;

/// <summary>
/// TCP connection implementation
/// </summary>
public class TcpConnection : IConnection
{
    internal readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly string _id;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed;

    public string Id => _id;
    public bool IsConnected => _client.Connected && !_disposed;

    public event EventHandler? Disconnected;

    public TcpConnection(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _id = Guid.NewGuid().ToString();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task SendMessageAsync(GabpMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsConnected)
            throw new InvalidOperationException("Connection is not active");

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {jsonBytes.Length}\r\nContent-Type: application/json\r\n\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);

        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        await _stream.WriteAsync(headerBytes, combinedToken);
        await _stream.WriteAsync(jsonBytes, combinedToken);
        await _stream.FlushAsync(combinedToken);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cancellationTokenSource.Cancel();
        
        try
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Ignore exceptions in event handlers
        }

        _stream?.Dispose();
        _client?.Close();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// TCP transport implementation for GABP
/// </summary>
public class TcpTransport : ITransport
{
    private readonly int _port;
    private TcpListener? _listener;
    private bool _disposed;
    private bool _running;

    public event EventHandler<ConnectionEstablishedEventArgs>? ConnectionEstablished;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public TcpTransport(int port = 0)
    {
        _port = port;
    }

    public int Port { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            throw new InvalidOperationException("Transport is already running");

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _running = true;

        // Start accepting connections in background
        _ = Task.Run(async () => await AcceptConnectionsAsync(cancellationToken), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_running) return Task.CompletedTask;

        _running = false;
        _listener?.Stop();
        return Task.CompletedTask;
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                var connection = new TcpConnection(tcpClient);
                
                ConnectionEstablished?.Invoke(this, new ConnectionEstablishedEventArgs(connection));
                
                // Start reading messages from this connection
                _ = Task.Run(async () => await ReadMessagesAsync(connection, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception)
            {
                // Log error and continue
                if (_running)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }

    private async Task ReadMessagesAsync(TcpConnection connection, CancellationToken cancellationToken)
    {
        var stream = connection._client.GetStream();
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        try
        {
            while (connection.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);

                // Process complete messages
                await ProcessMessagesAsync(connection, messageBuffer, cancellationToken);
            }
        }
        catch (Exception)
        {
            // Connection lost or error occurred
        }
        finally
        {
            connection.Dispose();
        }
    }

    private Task ProcessMessagesAsync(TcpConnection connection, StringBuilder buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var content = buffer.ToString();
            var headerEnd = content.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            
            if (headerEnd == -1) break; // No complete header yet

            var headerText = content[..headerEnd];
            var contentLengthMatch = System.Text.RegularExpressions.Regex.Match(
                headerText, @"Content-Length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!contentLengthMatch.Success) break;

            if (!int.TryParse(contentLengthMatch.Groups[1].Value, out var contentLength)) break;

            var messageStart = headerEnd + 4;
            if (content.Length < messageStart + contentLength) break; // Incomplete message

            var messageJson = content.Substring(messageStart, contentLength);
            
            try
            {
                var message = ParseMessage(messageJson);
                if (message != null)
                {
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(connection, message));
                }
            }
            catch (Exception)
            {
                // Invalid JSON or message format
            }

            // Remove processed message from buffer
            buffer.Remove(0, messageStart + contentLength);
        }

        return Task.CompletedTask;
    }

    private static GabpMessage? ParseMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeProperty))
            return null;

        var type = typeProperty.GetString();
        
        return type switch
        {
            "request" => JsonSerializer.Deserialize<GabpRequest>(json),
            "response" => JsonSerializer.Deserialize<GabpResponse>(json),
            "event" => JsonSerializer.Deserialize<GabpEvent>(json),
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _ = StopAsync();
        _listener?.Stop();
    }
}