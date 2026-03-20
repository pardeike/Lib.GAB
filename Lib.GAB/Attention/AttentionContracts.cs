using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Lib.GAB.Attention
{
    /// <summary>
    /// Canonical method and event identifiers for additive GABP attention support.
    /// </summary>
    public static class AttentionProtocol
    {
        public const string CurrentMethod = "attention/current";
        public const string AckMethod = "attention/ack";
        public const string OpenedChannel = "attention/opened";
        public const string UpdatedChannel = "attention/updated";
        public const string ClearedChannel = "attention/cleared";
    }

    /// <summary>
    /// Summarized representative entry associated with an attention item.
    /// </summary>
    public class AttentionSample
    {
        [JsonProperty("level")]
        public string Level { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("repeatCount")]
        public int RepeatCount { get; set; }

        [JsonProperty("latestSequence")]
        public long LatestSequence { get; set; }

        public AttentionSample Clone()
        {
            return new AttentionSample
            {
                Level = Level,
                Message = Message,
                RepeatCount = RepeatCount,
                LatestSequence = LatestSequence
            };
        }
    }

    /// <summary>
    /// Current blocking or advisory attention item surfaced by the game integration.
    /// </summary>
    public class AttentionItem
    {
        [JsonProperty("attentionId")]
        public string AttentionId { get; set; } = string.Empty;

        [JsonProperty("state")]
        public string State { get; set; } = "open";

        [JsonProperty("severity")]
        public string Severity { get; set; } = "error";

        [JsonProperty("blocking")]
        public bool Blocking { get; set; }

        [JsonProperty("stateInvalidated")]
        public bool StateInvalidated { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonProperty("causalOperationId", NullValueHandling = NullValueHandling.Ignore)]
        public string CausalOperationId { get; set; }

        [JsonProperty("causalMethod", NullValueHandling = NullValueHandling.Ignore)]
        public string CausalMethod { get; set; }

        [JsonProperty("openedAtSequence")]
        public long OpenedAtSequence { get; set; }

        [JsonProperty("latestSequence")]
        public long LatestSequence { get; set; }

        [JsonProperty("diagnosticsCursor", NullValueHandling = NullValueHandling.Ignore)]
        public long? DiagnosticsCursor { get; set; }

        [JsonProperty("totalUrgentEntries")]
        public int TotalUrgentEntries { get; set; }

        [JsonProperty("sample", NullValueHandling = NullValueHandling.Ignore)]
        public List<AttentionSample> Sample { get; set; } = new List<AttentionSample>();

        public AttentionItem Clone()
        {
            return new AttentionItem
            {
                AttentionId = AttentionId,
                State = State,
                Severity = Severity,
                Blocking = Blocking,
                StateInvalidated = StateInvalidated,
                Summary = Summary,
                CausalOperationId = CausalOperationId,
                CausalMethod = CausalMethod,
                OpenedAtSequence = OpenedAtSequence,
                LatestSequence = LatestSequence,
                DiagnosticsCursor = DiagnosticsCursor,
                TotalUrgentEntries = TotalUrgentEntries,
                Sample = Sample == null ? new List<AttentionSample>() : Sample.Select(entry => entry.Clone()).ToList()
            };
        }
    }

    /// <summary>
    /// Result shape for attention/current.
    /// </summary>
    public class AttentionCurrentResult
    {
        [JsonProperty("attention")]
        public AttentionItem Attention { get; set; }
    }

    /// <summary>
    /// Parameters for attention/ack.
    /// </summary>
    public class AttentionAckParams
    {
        [JsonProperty("attentionId")]
        public string AttentionId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result shape for attention/ack.
    /// </summary>
    public class AttentionAckResult
    {
        [JsonProperty("acknowledged")]
        public bool Acknowledged { get; set; }

        [JsonProperty("attentionId")]
        public string AttentionId { get; set; }

        [JsonProperty("currentAttention")]
        public AttentionItem CurrentAttention { get; set; }
    }
}
