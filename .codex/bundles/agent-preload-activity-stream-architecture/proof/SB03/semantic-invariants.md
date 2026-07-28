# SB03 Canonical Provider Snapshot Semantic Invariants

## Governed invariant contract

- Invariant ID: `SB03-PREP-001`
- Source raw note: prepared agent data should be reusable without retaining live or
  secret state, and a concurrent update must never publish a forgotten stale
  snapshot.
- Expected behavior: same-key/version callers share one immutable preparation load;
  invalidation fences a superseded completion and each waiter owns only its wait
  cancellation.
- Disallowed shallow implementation: cache mutable/live runtime objects, link shared
  factory cancellation to the first waiter, or complete a superseded entry after
  invalidation.
- Failing-first test: the controlled shallow mutation is killed by
  `bundle://proof/SB03/transcripts/controlled-stale-completion-red-green.txt`.
- Passing test: the restored production fence passes the same focused test in
  `bundle://proof/SB03/transcripts/controlled-stale-completion-red-green.txt`; the
  wider provider/preparation results are indexed by `bundle://proof/SB03/manifest.md`.
- Changed source files: the production fence is
  `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationCache.cs`;
  the complete changed-file inventory is in `bundle://proof/SB03/manifest.md`.
- Production assertions: entry replacement occurs under the cache lock, only the
  exact current entry may commit, and cancellation/disposal happens outside the
  publication decision.
- Red-team negative case: remove the `isCurrent` commit guard while invalidating a
  blocked load; the original waiter must not receive the stale blueprint.
- Downstream dependency check: A4-A7 reopen if a stale blueprint publishes or a
  cached value contains live/session/credential/authorization state.

## SB03-PROVIDER-001 — One runtime source of truth

The integrated runtime loads provider profiles only from
`Workspace_ProviderProfiles` through the no-tracking database loader and the explicit
synthetic Remote Ollama fallback. The file catalog remains a UI/import projection and
cannot resurrect a deleted runtime provider.

Positive proof:

- `DatabaseProviderRuntimeProfileSnapshotLoader` is the integrated loader.
- `WorkspaceBackedAgentProviderProfileRegistry` no longer implements
  `IProviderRuntimeProfileSource`.
- `Canonical_provider_configuration_overrides_divergent_catalog_shadow` passes.
- `Deleted_selected_provider_returns_typed_configuration_change` passes.

## SB03-PROVIDER-002 — Atomic immutable publication and bounded capture

Ready state is an immutable dictionary of immutable provider leases. Readers use one
volatile provider-state read and dictionary lookup, followed by the existing
in-memory database-runtime identity check. Publication uses a short state lock after
the complete immutable replacement is prepared.

Positive proof:

- `Warm_reads_do_not_reenter_snapshot_loader` passes and records one initial
  `LoadAllAsync`, zero per-provider loader calls for later list/get/capture reads.
- The startup matrix records zero provider registry gets and four bounded snapshot
  captures in every scenario. SB05 later measured the current revision-probe behavior
  as one SQL command for an unchanged non-synthetic provider, zero for a synthetic
  provider, and three across the changed-provider scenario. Capture is therefore not
  described as database-free.

Adversarial condition:

- The integration harness distinguishes `ProviderProfileGet` from
  `ProviderSnapshotCapture`; treating both as I/O would hide the actual architecture.

## SB03-PROVIDER-003 — Profile isolation and stale-publication fencing

Snapshot state carries active database profile ID, fingerprint, and generation.
Database-switch notification immediately publishes `NotReady` and advances a
publication fence. A slower rebuild can publish only when both the fence and runtime
identity still match.

Positive proof:

- `Superseded_profile_rebuild_cannot_publish_old_data` passes.
- `Cancelled_initialization_remains_not_ready` passes.

Adversarial condition:

- A blocked old-profile loader is released after the profile switch. Its result is
  rejected and a capture for the new profile throws the typed `NotReady` exception.

## SB03-PROVIDER-004 — Explicit failure, deletion, and configuration invalidation

Missing or deleted selected providers return a typed
`ProviderConfigurationChanged` use-time result. A failed post-commit provider
projection faults the complete canonical snapshot and fails closed; it is never
silently treated as a deletion or served from the catalog.

Positive proof:

- `Use_time_validation_returns_typed_stale_reasons` passes.
- `Deleted_selected_provider_returns_typed_configuration_change` passes.
- `Committed_projection_failure_is_an_explicit_fault` passes and preserves the
  original exception as the typed snapshot-unavailable inner exception.

## SB03-PROVIDER-005 — Provider-local changes

The blueprint version contains the selected provider fingerprint. Updating a different
provider does not invalidate or rebuild the selected provider blueprint.

Positive proof:

- `Unrelated_provider_update_does_not_invalidate_selected_provider_blueprint` passes
  and verifies reference reuse plus `Current` use-time validation.

## SB03-PROVIDER-006 — No secret payload or live runtime object retention

The snapshot retains normalized provider configuration and only the secret-reference
identity (`secret:{Guid}` or environment-variable name). It does not resolve or retain
secret payloads, clients, `DbContext`, live agents, tools, sessions, authorization
results, approvals, or context-contributor output. Credentials remain resolved for
each dispatch.

Source proof:

- Database contexts are scoped to loader calls and disposed before publication.
- The mapper converts `ApiKeySecretId` to a reference identity, never a secret value.
- The immutable lease contains only `ProviderProfile` plus its non-secret
  configuration fingerprint.

## SB03-PROVIDER-007 — Post-commit projection ordering

Provider save/delete observers execute only after the owning `SaveChangesAsync`
completes. Save reloads the canonical database row before atomic upsert. Delete
atomically removes the provider. Projection failure faults the snapshot without
turning a committed database operation into a false rollback report.

Source proof:

- Both Workspace provider mutation paths and AgentFramework provider-registry paths
  call the narrow typed observer after database commit.
- Observer callbacks use `CancellationToken.None` after commit so caller cancellation
  cannot leave canonical in-memory state knowingly stale.

## Progression statement

This evidence supports the provider/preparation portion of A3. The consolidated gate
is `PASS with two A3 P2 follow-ups`; see `bundle://proof/SB03/a3-decision.md`.
