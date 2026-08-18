# Profile lifecycle and operation scope

## Required scope

Every public application query/command captures:

```text
DatabaseProfileId
DatabaseFingerprint
DatabaseGeneration
```

before its first canonical read. That identity is verified:

- before provider resolution;
- before durable claim;
- before/after provider dispatch where relevant;
- before every command commit;
- before returning a result that claims current-profile authority.

## Profile switch behavior

- In-flight dispatcher work receives a profile-lifetime cancellation signal.
- A command whose generation changed before commit fails closed and commits no cross-generation state.
- A provider result received after switch is not committed into the new profile.
- SSE streams for the old profile close through existing `ProfileBoundedReplayEventStream`.
- Reconciliation occurs within the operation's original database/profile store, never by looking up the
  same ID in the newly active profile.
- Cache/snapshot invalidation is explicit.

## Implementation boundary

Prefer an `ILlmChatOperationScopeFactory` or similarly narrow factory that creates a scope containing
profile identity, current database services and lifetime token. Do not scatter ad hoc calls to
`IDatabaseRuntimeState.GetSnapshot()` throughout repositories.
