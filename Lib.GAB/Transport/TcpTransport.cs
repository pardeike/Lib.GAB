using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Lib.GAB.Protocol;

namespace Lib.GAB.Transport
{
    /// <summary>
    /// TCP connection implementation
    /// </summary>
    public class TcpConnection : IConnection
    {
        internal readonly TcpClient _client;
        private readonly Stream _stream;
        private readonly string _id;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _sendLock;
        private bool _disposed;

        public string Id => _id;
        public bool IsConnected => _client.Connected && !_disposed;

        public event EventHandler Disconnected;

        public TcpConnection(TcpClient client)
            : this(client, client?.GetStream() ?? throw new ArgumentNullException(nameof(client)))
        {
        }

        internal TcpConnection(TcpClient client, Stream stream)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _id = Guid.NewGuid().ToString();
            _cancellationTokenSource = new CancellationTokenSource();
            _sendLock = new SemaphoreSlim(1, 1);
        }

        public async Task SendMessageAsync(GabpMessage message, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_disposed || !IsConnected)
                throw new InvalidOperationException("Connection is not active");

            var json = JsonConvert.SerializeObject(message);

            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var header = $"Content-Length: {jsonBytes.Length}\r\nContent-Type: application/json\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var frameBytes = new byte[headerBytes.Length + jsonBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, frameBytes, 0, headerBytes.Length);
            Buffer.BlockCopy(jsonBytes, 0, frameBytes, headerBytes.Length, jsonBytes.Length);

            using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token))
            {
                var combinedToken = linkedTokenSource.Token;
                await _sendLock.WaitAsync(combinedToken).ConfigureAwait(false);
                try
                {
                    if (_disposed || !IsConnected)
                        throw new InvalidOperationException("Connection is not active");

                    await _stream.WriteAsync(frameBytes, 0, frameBytes.Length, combinedToken).ConfigureAwait(false);
                    await _stream.FlushAsync(combinedToken).ConfigureAwait(false);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
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
        private TcpListener _listener;
        private bool _disposed;
        private bool _running;

        public event EventHandler<ConnectionEstablishedEventArgs> ConnectionEstablished;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public TcpTransport(int port = 0)
        {
            _port = port;
        }

        public int Port { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_running)
                throw new InvalidOperationException("Transport is already running");

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;

            // Start accepting connections in background
            var task = Task.Run(async () => await AcceptConnectionsAsync(cancellationToken), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_running) return Task.FromResult(0);

            _running = false;
            _listener?.Stop();
            return Task.FromResult(0);
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var connection = new TcpConnection(tcpClient);
                    
                    ConnectionEstablished?.Invoke(this, new ConnectionEstablishedEventArgs(connection));
                    
                    // Start reading messages from this connection
                    var messageTask = Task.Run(async () => await ReadMessagesAsync(connection, cancellationToken), cancellationToken);
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
            var messageBuffer = new MemoryStream();

            try
            {
                while (connection.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0) break;

                    // Frames are delimited in bytes: Content-Length counts UTF-8
                    // bytes, and a multibyte character can straddle two reads. So
                    // buffer raw bytes and decode each body only once it is whole.
                    messageBuffer.Write(buffer, 0, bytesRead);

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

        private Task ProcessMessagesAsync(TcpConnection connection, MemoryStream buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                var content = buffer.GetBuffer();
                var length = (int)buffer.Length;
                var headerEnd = IndexOfHeaderEnd(content, length);
                
                if (headerEnd == -1) break; // No complete header yet

                var headerText = Encoding.ASCII.GetString(content, 0, headerEnd);
                var contentLengthIndex = headerText.IndexOf("Content-Length:", StringComparison.OrdinalIgnoreCase);
                
                if (contentLengthIndex == -1) break;

                var startIndex = contentLengthIndex + "Content-Length:".Length;
                var endIndex = headerText.IndexOf('\r', startIndex);
                if (endIndex == -1) endIndex = headerText.IndexOf('\n', startIndex);
                if (endIndex == -1) endIndex = headerText.Length;

                var contentLengthStr = headerText.Substring(startIndex, endIndex - startIndex).Trim();
                if (!int.TryParse(contentLengthStr, out var contentLength)) break;

                var messageStart = headerEnd + 4;
                if (length < messageStart + contentLength) break; // Incomplete message

                var messageJson = Encoding.UTF8.GetString(content, messageStart, contentLength);
                
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
                var consumed = messageStart + contentLength;
                var remaining = length - consumed;
                Buffer.BlockCopy(content, consumed, content, 0, remaining);
                buffer.SetLength(remaining);
                buffer.Position = remaining;
            }

            return Task.FromResult(0);
        }

        private static int IndexOfHeaderEnd(byte[] content, int length)
        {
            for (var i = 0; i + 3 < length; i++)
            {
                if (content[i] == (byte)'\r' && content[i + 1] == (byte)'\n'
                    && content[i + 2] == (byte)'\r' && content[i + 3] == (byte)'\n')
                {
                    return i;
                }
            }

            return -1;
        }

        private static GabpMessage ParseMessage(string json)
        {
            var messageDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (messageDict == null || !messageDict.ContainsKey("type"))
                return null;

            var type = messageDict["type"].ToString();
            switch (type)
            {
                case "request":
                    return JsonConvert.DeserializeObject<GabpRequest>(json);
                case "response":
                    return JsonConvert.DeserializeObject<GabpResponse>(json);
                case "event":
                    return JsonConvert.DeserializeObject<GabpEvent>(json);
                default:
                    return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_running)
            {
                _running = false;
                _listener?.Stop();
            }
        }
    }
}
