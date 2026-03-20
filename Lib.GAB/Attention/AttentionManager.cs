using System;
using System.Threading.Tasks;
using Lib.GAB.Events;

namespace Lib.GAB.Attention
{
    /// <summary>
    /// Default attention manager implementation backed by the event manager.
    /// </summary>
    public class AttentionManager : IAttentionManager
    {
        private readonly IEventManager _eventManager;
        private readonly bool _isEnabled;
        private readonly object _lockObject = new object();
        private AttentionItem _currentAttention;

        public AttentionManager(IEventManager eventManager, bool isEnabled)
        {
            _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
            _isEnabled = isEnabled;
        }

        public bool IsEnabled => _isEnabled;

        public AttentionItem GetCurrent()
        {
            lock (_lockObject)
            {
                return _currentAttention?.Clone();
            }
        }

        public async Task<AttentionItem> PublishAsync(AttentionItem attention, DateTimeOffset? timestamp = null)
        {
            EnsureEnabled();

            if (attention == null)
                throw new ArgumentNullException(nameof(attention));
            if (string.IsNullOrWhiteSpace(attention.AttentionId))
                throw new ArgumentException("Attention item must have a non-empty attentionId.", nameof(attention));

            AttentionItem normalized;
            string channel;

            lock (_lockObject)
            {
                var current = _currentAttention;
                normalized = NormalizePublishedAttention(attention, current);
                channel = current != null &&
                    string.Equals(current.AttentionId, normalized.AttentionId, StringComparison.Ordinal)
                    ? AttentionProtocol.UpdatedChannel
                    : AttentionProtocol.OpenedChannel;

                _currentAttention = normalized.Clone();
            }

            await _eventManager.EmitEventAsync(channel, normalized, timestamp);
            return normalized.Clone();
        }

        public async Task<AttentionItem> ClearAsync(AttentionItem clearedAttention = null, DateTimeOffset? timestamp = null)
        {
            EnsureEnabled();

            AttentionItem cleared;

            lock (_lockObject)
            {
                if (_currentAttention == null)
                    return null;

                cleared = NormalizeClearedAttention(clearedAttention, _currentAttention);
                _currentAttention = null;
            }

            await _eventManager.EmitEventAsync(AttentionProtocol.ClearedChannel, cleared, timestamp);
            return cleared.Clone();
        }

        public async Task<AttentionAckResult> AcknowledgeAsync(string attentionId, DateTimeOffset? timestamp = null)
        {
            EnsureEnabled();

            if (string.IsNullOrWhiteSpace(attentionId))
                throw new ArgumentException("Attention acknowledgement requires a non-empty attentionId.", nameof(attentionId));

            AttentionItem cleared = null;
            AttentionItem currentAttention = null;
            var acknowledged = false;

            lock (_lockObject)
            {
                if (_currentAttention != null &&
                    string.Equals(_currentAttention.AttentionId, attentionId, StringComparison.Ordinal))
                {
                    cleared = NormalizeClearedAttention(null, _currentAttention);
                    _currentAttention = null;
                    acknowledged = true;
                }
                else if (_currentAttention != null)
                {
                    currentAttention = _currentAttention.Clone();
                }
            }

            if (cleared != null)
            {
                await _eventManager.EmitEventAsync(AttentionProtocol.ClearedChannel, cleared, timestamp);
            }

            return new AttentionAckResult
            {
                Acknowledged = acknowledged,
                AttentionId = attentionId,
                CurrentAttention = currentAttention?.Clone()
            };
        }

        private void EnsureEnabled()
        {
            if (!_isEnabled)
                throw new InvalidOperationException("Attention support is not enabled for this server.");
        }

        private static AttentionItem NormalizePublishedAttention(AttentionItem incoming, AttentionItem current)
        {
            var normalized = incoming.Clone();
            normalized.State = "open";

            if (current != null &&
                string.Equals(current.AttentionId, normalized.AttentionId, StringComparison.Ordinal))
            {
                if (normalized.OpenedAtSequence == 0)
                    normalized.OpenedAtSequence = current.OpenedAtSequence;
                if (normalized.LatestSequence == 0)
                    normalized.LatestSequence = current.LatestSequence;
                if (normalized.TotalUrgentEntries == 0)
                    normalized.TotalUrgentEntries = current.TotalUrgentEntries;
            }
            else if (normalized.LatestSequence == 0)
            {
                normalized.LatestSequence = normalized.OpenedAtSequence;
            }

            return normalized;
        }

        private static AttentionItem NormalizeClearedAttention(AttentionItem clearedAttention, AttentionItem current)
        {
            var cleared = (clearedAttention ?? current).Clone();
            cleared.AttentionId = string.IsNullOrWhiteSpace(cleared.AttentionId) ? current.AttentionId : cleared.AttentionId;
            cleared.State = "cleared";
            cleared.Blocking = false;

            if (cleared.OpenedAtSequence == 0)
                cleared.OpenedAtSequence = current.OpenedAtSequence;
            if (cleared.LatestSequence == 0)
                cleared.LatestSequence = current.LatestSequence;
            if (cleared.TotalUrgentEntries == 0)
                cleared.TotalUrgentEntries = current.TotalUrgentEntries;
            if (string.IsNullOrWhiteSpace(cleared.Severity))
                cleared.Severity = current.Severity;
            if (string.IsNullOrWhiteSpace(cleared.Summary))
                cleared.Summary = current.Summary;
            if (string.IsNullOrWhiteSpace(cleared.CausalOperationId))
                cleared.CausalOperationId = current.CausalOperationId;
            if (string.IsNullOrWhiteSpace(cleared.CausalMethod))
                cleared.CausalMethod = current.CausalMethod;
            if (!cleared.DiagnosticsCursor.HasValue)
                cleared.DiagnosticsCursor = current.DiagnosticsCursor;
            if ((cleared.Sample == null || cleared.Sample.Count == 0) && current.Sample != null)
                cleared.Sample = current.Sample;

            return cleared;
        }
    }
}
