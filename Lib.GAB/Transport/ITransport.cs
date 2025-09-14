using System;
using System.Threading;
using System.Threading.Tasks;
using Lib.GAB.Protocol;

namespace Lib.GAB.Transport
{
    /// <summary>
    /// Represents a connection to a bridge client
    /// </summary>
    public interface IConnection : IDisposable
    {
        /// <summary>
        /// Unique identifier for this connection
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Whether the connection is still active
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Send a message to the bridge
        /// </summary>
        Task SendMessageAsync(GabpMessage message, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Event raised when connection is lost
        /// </summary>
        event EventHandler Disconnected;
    }

    /// <summary>
    /// Transport layer abstraction for GABP communication
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// Start listening for connections
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Stop listening for connections
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Event raised when a new connection is established
        /// </summary>
        event EventHandler<ConnectionEstablishedEventArgs> ConnectionEstablished;

        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
    }

    /// <summary>
    /// Event args for connection established
    /// </summary>
    public class ConnectionEstablishedEventArgs : EventArgs
    {
        public IConnection Connection { get; }

        public ConnectionEstablishedEventArgs(IConnection connection)
        {
            Connection = connection;
        }
    }

    /// <summary>
    /// Event args for message received
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        public IConnection Connection { get; }
        public GabpMessage Message { get; }

        public MessageReceivedEventArgs(IConnection connection, GabpMessage message)
        {
            Connection = connection;
            Message = message;
        }
    }
}