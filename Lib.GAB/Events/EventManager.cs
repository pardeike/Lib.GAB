using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lib.GAB.Protocol;
using Lib.GAB.Transport;

namespace Lib.GAB.Events
{
    /// <summary>
    /// Default implementation of the event manager
    /// </summary>
    public class EventManager : IEventManager
    {
        private readonly ConcurrentDictionary<string, EventChannelState> _channels = new ConcurrentDictionary<string, EventChannelState>();
        private readonly ConcurrentDictionary<string, HashSet<IConnection>> _subscriptions = new ConcurrentDictionary<string, HashSet<IConnection>>();
        private readonly object _lockObject = new object();

        private class EventChannelState
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; }
            public int SequenceNumber;
            public HashSet<IConnection> Subscribers { get; set; } = new HashSet<IConnection>();
        }

        public async Task<List<string>> SubscribeAsync(IConnection connection, List<string> channels)
        {
            var subscribed = new List<string>();

            lock (_lockObject)
            {
                foreach (var channel in channels)
                {
                    if (!_channels.ContainsKey(channel))
                        continue; // Channel doesn't exist

                    var channelState = _channels[channel];
                    channelState.Subscribers.Add(connection);

                    if (!_subscriptions.ContainsKey(connection.Id))
                        _subscriptions[connection.Id] = new HashSet<IConnection>();
                    
                    _subscriptions[connection.Id].Add(connection);
                    subscribed.Add(channel);
                }

                // Clean up when connection disconnects
                connection.Disconnected += (_, __) => CleanupConnection(connection);
            }

            return await Task.FromResult(subscribed);
        }

        public async Task<List<string>> UnsubscribeAsync(IConnection connection, List<string> channels)
        {
            var unsubscribed = new List<string>();

            lock (_lockObject)
            {
                foreach (var channel in channels)
                {
                    EventChannelState channelState;
                    if (_channels.TryGetValue(channel, out channelState))
                    {
                        if (channelState.Subscribers.Remove(connection))
                        {
                            unsubscribed.Add(channel);
                        }
                    }
                }
            }

            return await Task.FromResult(unsubscribed);
        }

        public List<string> GetAvailableChannels()
        {
            return _channels.Keys.ToList();
        }

        public void RegisterChannel(string channel, string description = null)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel name cannot be null or empty", nameof(channel));

            _channels[channel] = new EventChannelState
            {
                Name = channel,
                Description = description,
                SequenceNumber = 0,
                Subscribers = new HashSet<IConnection>()
            };
        }

        public bool UnregisterChannel(string channel)
        {
            lock (_lockObject)
            {
                EventChannelState channelState;
                if (_channels.TryRemove(channel, out channelState))
                {
                    // Remove all subscriptions for this channel
                    foreach (var connection in channelState.Subscribers.ToList())
                    {
                        HashSet<IConnection> connectionChannels;
                        if (_subscriptions.TryGetValue(connection.Id, out connectionChannels))
                        {
                            connectionChannels.Remove(connection);
                            if (connectionChannels.Count == 0)
                            {
                                HashSet<IConnection> removedChannels;
                                _subscriptions.TryRemove(connection.Id, out removedChannels);
                            }
                        }
                    }
                    return true;
                }
                return false;
            }
        }

        public async Task EmitEventAsync(string channel, object payload, DateTimeOffset? timestamp = null)
        {
            EventChannelState channelState;
            if (!_channels.TryGetValue(channel, out channelState))
                return; // Channel doesn't exist

            var eventMessage = new GabpEvent
            {
                Channel = channel,
                Sequence = Interlocked.Increment(ref channelState.SequenceNumber),
                Payload = payload,
                Timestamp = timestamp ?? DateTimeOffset.UtcNow
            };

            // Get current subscribers (copy to avoid collection modification issues)
            var subscribers = channelState.Subscribers.ToList();
            
            // Send to all subscribers
            var tasks = subscribers.Select(async connection =>
            {
                try
                {
                    if (connection.IsConnected)
                    {
                        await connection.SendMessageAsync(eventMessage);
                    }
                    else
                    {
                        // Connection is dead, remove it
                        CleanupConnection(connection);
                    }
                }
                catch
                {
                    // Log error and remove connection
                    CleanupConnection(connection);
                }
            });

            await Task.WhenAll(tasks);
        }

        public int GetSubscriberCount(string channel)
        {
            EventChannelState channelState;
            return _channels.TryGetValue(channel, out channelState) 
                ? channelState.Subscribers.Count 
                : 0;
        }

        private void CleanupConnection(IConnection connection)
        {
            lock (_lockObject)
            {
                // Remove connection from all channel subscriptions
                foreach (var channelState in _channels.Values)
                {
                    channelState.Subscribers.Remove(connection);
                }

                // Remove connection from subscriptions tracking
                HashSet<IConnection> removedSubscriptions;
                _subscriptions.TryRemove(connection.Id, out removedSubscriptions);
            }
        }
    }
}