# Proven verification and delivery feedback

Evidence: CDA-UI-SEAMS-PROVIDERS-02E, exact postcondition tests, real database Replace reconciliation, rendered callback failure/reconstruction and fresh 850-case owning checkpoint.

1. Verification of a replacement operation compares the exact authoritative selected set, not merely containment. Preserve independent identity, revision, timestamp and status evidence; retired items are not selected membership.
2. Capture immutable intended postconditions before an asynchronous mutation. A changed revision alone cannot prove the intended result. Distinguish desired current state, exact unchanged before state and contradictory/insufficient evidence. Current satisfaction establishes safe continuation, not historical causation.
3. Resolving an authoritative mutation and delivering its change to a parent are separate states. Retain the attempt until completed parent reconciliation is acknowledged. Callback failure needs visible delivery retry without replaying the mutation.
4. Busy and delivery ownership include attempt identity, target identity and component generation. Serialize concurrent delivery; a stale owner cannot acknowledge another target. A receiver acknowledgement can survive sender teardown without repeating its completed reconciliation.
5. A callback that may be retried must perform idempotent reconciliation, not an unguarded external mutation. Circuit-scoped acknowledgement is not a durable distributed exactly-once guarantee.
6. Terminal completion removes all matching attempt/delivery bookkeeping. Pending work stays retained; older completion or retention cannot erase or resurrect a newer attempt. Test this through public behavior, not private collection counts.

These findings refine the existing seam rules without prescribing provider-specific types, a universal event bus, a generic outbox or a new dependency layer.
