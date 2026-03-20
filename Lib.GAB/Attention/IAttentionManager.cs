using System;
using System.Threading.Tasks;

namespace Lib.GAB.Attention
{
    /// <summary>
    /// Manages the current game attention item and emits lifecycle events.
    /// </summary>
    public interface IAttentionManager
    {
        /// <summary>
        /// Whether attention support is enabled on the enclosing server.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Get the current open attention item, if any.
        /// </summary>
        AttentionItem GetCurrent();

        /// <summary>
        /// Publish a current attention item. Reuses the same attentionId for updates.
        /// </summary>
        Task<AttentionItem> PublishAsync(AttentionItem attention, DateTimeOffset? timestamp = null);

        /// <summary>
        /// Clear the current attention item and emit the cleared lifecycle event.
        /// </summary>
        Task<AttentionItem> ClearAsync(AttentionItem clearedAttention = null, DateTimeOffset? timestamp = null);

        /// <summary>
        /// Acknowledge the current attention item when the attentionId matches.
        /// </summary>
        Task<AttentionAckResult> AcknowledgeAsync(string attentionId, DateTimeOffset? timestamp = null);
    }
}
