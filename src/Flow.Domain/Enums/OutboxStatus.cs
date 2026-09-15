namespace Flow.Domain.Enums;

public enum OutboxStatus
{
    Pending,
    Dispatched,
    Failed,
    DeadLettered,

    /// <summary>
    /// Claimed by a worker and being delivered right now.
    ///
    /// The state exists so that two replicas cannot both pick up the same message: a claim
    /// moves it here atomically, and only the worker that moved it gets to send. It is
    /// paired with a lease, because a worker that dies mid-delivery would otherwise leave
    /// the message stuck here forever.
    /// </summary>
    Processing
}
