using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lib.GAB.Transport;

namespace Lib.GAB.Events
{
    /// <summary>
    /// Manages event subscriptions and broadcasting for GABP
    /// </summary>
    public interface IEventManager
    {
        /// <summary>
        /// Subscribe a connection to one or more event channels
        /// </summary>
        Task<List<string>> SubscribeAsync(IConnection connection, List<string> channels);

        /// <summary>
        /// Unsubscribe a connection from one or more event channels
        /// </summary>
        Task<List<string>> UnsubscribeAsync(IConnection connection, List<string> channels);

        /// <summary>
        /// Get all available event channels
        /// </summary>
        List<string> GetAvailableChannels();

        /// <summary>
        /// Register an available event channel
        /// </summary>
        void RegisterChannel(string channel, string description = null);

        /// <summary>
        /// Unregister an event channel
        /// </summary>
        bool UnregisterChannel(string channel);

        /// <summary>
        /// Emit an event to all subscribed connections
        /// </summary>
        Task EmitEventAsync(string channel, object payload, DateTimeOffset? timestamp = null);

        /// <summary>
        /// Get subscribers count for a channel
        /// </summary>
        int GetSubscriberCount(string channel);
    }

    /// <summary>
    /// Information about an event channel
    /// </summary>
    public class EventChannelInfo
    {
        /// <summary>
        /// Channel name (e.g., "player/move")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Number of current subscribers
        /// </summary>
        public int SubscriberCount { get; set; }
    }
}