# Migration and compatibility strategy

## General rule

Use expand-migrate-contract. Do not switch every caller and persistence shape in one commit.

## Context metadata

### Current

`candoitall.agent-chat-context.v1` records snapshot information in invocation metadata and binds the transient payload by digest.

### Target v2

Add separate sections:

```json
{
  "schema": "candoitall.agent-turn-context.v2",
  "observation": {
    "scopeId": "...",
    "sourceKind": "...",
    "sourceId": "...",
    "surface": "...",
    "view": "...",
    "version": 42,
    "capturedAtUtc": "...",
    "digest": "..."
  },
  "conversation": {
    "mode": "FollowCurrentSurface",
    "bindingRevision": 4,
    "transitionKind": "ViewChanged"
  },
  "authority": {
    "authorityId": "...",
    "workspaceScopeKind": "Project",
    "workspaceScopeKey": "...",
    "policyVersion": "...",
    "policyFingerprint": "..."
  }
}
```

Persist a typed `AgentTurnContextReference` and `AgentExecutionAuthorityRecord` when feasible. Metadata remains a projection for diagnostics, not the only authority record.

### Compatibility sequence

1. Add v2 writers and readers.
2. Keep v1 reader for existing runs.
3. Write v2 for new floating turns.
4. Approval continuation of v1 runs follows the original v1 lease behavior.
5. Do not reinterpret old v1 scope under new authority rules.
6. Remove v1 write path after tests and one release boundary.
7. Retain v1 read support while persisted old runs may exist.

## Runtime interface

1. Introduce narrow ports.
2. Make the old `IAgentRuntime` facade delegate.
3. Migrate production callers.
4. Add source assertion blocking new callers.
5. Delete facade in SB18 after SB17 stabilization and caller proof.

## MAF runtime class

1. Extract direct collaborators with behavior-preserving tests.
2. Make `MafAgentRuntime` a pure facade.
3. Migrate DI registrations to narrow adapters.
4. Delete facade when no production caller remains.

## Transient context

1. Introduce `AgentRuntimeModelContext` and authority snapshot.
2. Adapt old `AgentRuntimeTransientContext` into the new pair.
3. Floating chat writes the new pair.
4. Runtime option building requires authority.
5. Remove `WorkspaceScope` from the model-context record.
6. Preserve attachment lease and digest behavior.

## Secret resolver

1. Create narrow abstraction project.
2. Move contracts/constants.
3. Make Modules.Security implement the moved interface.
4. Migrate MAF/MCP callers.
5. Remove MAF project reference to Modules.Security.
6. Delete duplicate old contracts.

## Process recovery

1. Add generic runtime failure evidence.
2. Add recovery policy contract.
3. Move process recovery implementation and tests.
4. Route recovery through Core execution coordination.
5. Delete MAF process recovery code.
6. Add source assertions.
7. Confirm recovered output still enters the normal process completion coordinator.

## Runtime state envelope

Existing raw serialized state remains readable through a legacy adapter. New writes use a versioned envelope. No automatic best-effort deserialization of an incompatible envelope is allowed.

## Database/file compatibility

New optional record properties must:

- have safe defaults,
- deserialize old JSON,
- avoid changing equality/hash behavior unexpectedly,
- have migration tests for file store and database store,
- avoid serializing opaque attachment objects.

## Revision 2 cutover requirements

- Use the single-path strangler rules in `architecture/12-high-risk-cutover-playbook.md`.
- Add a compatibility evaluator result rather than boolean restore success.
- Record adapter/schema/provider/model/toolset/context-policy fingerprints for runtime-state decisions.
- Migrate approvals with exact pending-set revision/fingerprint.
- Retain legacy readers while historical/waiting records can exist; remove legacy writers first.
- Add the lightweight LLM port above provider runtime/driver infrastructure and migrate workflow callers without product scope inference.
- Run SB17 before deleting any facade, selector, or reader.
