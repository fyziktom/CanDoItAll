# SB02 — Validated Provider Revision Projections

## Status

- `Ready` for later execution; not started.

## Objective

Reduce revision query/full profile-copying overhead without weakening current validated availability for local or shared providers.

## Covered Inputs

- N001/N004; R02/R04/R08/R10; recommendation4 excluded.

## Prerequisites

- Future execution authorization, Phase0 baseline and valid isolated PostgreSQL test-server ownership.
- Characterize single/set/cold/warm probe semantics and full-load parity before edits.
- SB01 source proof is independent; integrated closure requires both.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs`

Symbols/ownership: DatabaseProviderRuntimeProfileSnapshotLoader LoadRevisionAsync/LoadRevisionsAsync/LoadAsync/MapPersonal/MapShared.

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs`

Symbols/ownership: structural and derived-cache validation versus effective profile materialization.

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderPublicationSnapshotReader.cs`

Symbols/ownership: bounded typed JSON and canonical publication revision validation.

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/RuntimeProjection/PersistedProviderProfileMapper.cs`

Symbols/ownership: local mapping/connector/metadata validation.

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/RuntimeProjection/ProviderRuntimeProfileSnapshotService.cs`

Symbols/ownership: synchronization, probe faults, retries and database-generation guards.

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/ProviderRuntimeProfileSnapshot.cs`

Symbols/ownership: existing ProviderConfigurationRevision readonly record struct.

- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`

Symbols/ownership: supported tracked token rotation.


## Deliverables

- Bounded typed projections and separation of existing validation from effective profile/model copying.
- Unchanged loader interface/revision algorithm and cache publication/fail-closed behavior.
- Real concrete-loader/relational query and corrupted-input tests, not mocks alone.

## Implementation Steps

1. Establish current local/shared validation/null/throw/disabled-profile behavior, including unchanged-token malformed data.
2. Use a typed bounded projection/join to avoid three serial full shared entity loads where it preserves cardinality/error behavior. Preserve exact local token and shared ID/token SHA256→GUID byte-order algorithm.
3. Reuse a **single existing** shared validation path before revision return; keep bounded JSON/schema/canonical revision/duplicate-field/derived cache checks. Do not construct/copy effective runtime models/prices/thinking metadata on unchanged revision probes.
4. Narrow local mapping only if connector/metadata/normalization/errors remain equivalent; otherwise retain current local mapping in this unit.
5. Preserve set lookup semantics (invalid omitted, valid operationally disabled retained), bounded publication-race retries, generation switching and dispatch-scoped secret resolution.
6. Verify actual query/materializer calls and functional equivalence with isolated DB tests, then snapshot/preparation composition.

## Dependency Impact

- Critical provider integrity foundation for integrated real local/shared conversations.
- Can run alongside SB01 with disjoint ownership; live baselines/deployments remain serial.

## Validation Depth

- Proof tier: Governed.
- Test project or non-test check: U/I SB02 rows in `plan/test-selection.md`; concrete relational loader plus architecture checks.
- Filter: seven unit class prefixes and two integration prefixes listed there.
- Selection reason: loader→validator→snapshot→preparation→credential dispatch contract.
- Expected discovered tests: 73unit+23integration source cases plus declared new corruption/projection cases; reconcile actual discovery and fail on zero.
- Invalidation keys: loader, validator, profile mapper, revision algorithm, source/import/profile relationships, DB generation, credential dispatch and test DB mode.
- Broad-gate decision: Not required; public revision/schema/DI/project-reference change reopens scope.
- Critical foundation: yes; concrete persisted shared graph through snapshot/preparation is the dependent-flow smoke.

## Acceptance Checklist

- Valid local/shared single/set revisions match full-load behavior; supported edits/deletes/relinks/source changes invalidate correctly.
- Prime valid shared lease, corrupt JSON without rotating any tokens, then single **and set** lookup cannot return the prior enabled lease. Cover duplicate metadata and forged derived BaseUrl/default model/capabilities.
- Invalid/missing/duplicate relationships, malformed/oversized/schema/hash snapshots, URI/credential/enum mismatches fail as before; valid unavailable profiles remain disabled with current semantics.
- Local unknown connector/metadata errors are not hidden by scalar token projection.
- Synthetic fallback enabled/disabled, probe failure/cancellation, race retries and database switch remain correct.
- Secret values are never cached into revision state; next dispatch still resolves rotated secret as before.
- Concrete loader has bounded query work and avoids unnecessary effective catalog copies; RecordingLoader mocks alone cannot pass.
- No token-only shared cache. Optional immutable validation caching is not the default and requires separate complete-content/state/scope proof before inclusion.

## Scope Exceptions

Out-of-band *valid* operational-state mutation with unchanged tokens may already reuse an old revision; this bundle does not promise a new raw-SQL invalidation guarantee. It must preserve the existing stronger rejection of malformed/inconsistent inputs even with unchanged tokens. Canonical hash verification is integrity validation, not an authenticated-signature claim.

## Proof Required

- `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`; source/test hashes, discovery/transcripts, query/copy measurements, exact corrupted-input outcomes.
- Full-load vs revision-path semantic parity; concrete relational SQL/materialization evidence and negative stale-lease tests.
- Source assertion that no validation was bypassed; anti-stub audit, credential secrecy and dependency gate.

## UI Composition Contract

No UI edits; retain existing readable model/selection behavior. Combined real provider/tool UI proof is owned by SB03.

## Browser Validation Logging

Record provider/model IDs and final source hashes for both-host UI matrix; mocked provider tests do not replace it.

## C# Architecture Impact

Local query/validation separation; no provider contract, schema or lifecycle redesign.

## Boundary Ownership

EF/query composition stays in outer Module; canonical validation in ProviderManagement; inner MAF gains no EF/workspace/shared-source dependency.

## Dependency Direction

Existing Module→ProviderManagement/Core remains; ProviderManagement must not acquire outer UI/module feature references.

## Pattern Decision

P02 typed projection plus shared existing validator; reject a second weaker validator or speculative cache/interface.

## Testability Contract

Concrete loader through isolated relational DB plus directly tested validators and existing snapshot recording-loader tests. Test extracted validation independently, not solely through whole host.

## Partial Class Policy

No new partial, nested behavior container, service locator or duplicated parser.

## Architecture Proof Required

Before/after project graph, ProviderManagement boundary tests, source ownership and direct validation tests. Any extraction removes duplicate old mapping validation while keeping one oracle.

## Progression Gate

- All integrity/parity/query/cache/dispatch/DB-isolation checks and architecture review pass before integrated closure.
- If unchanged-token corruption detection or local parity cannot be preserved, retain existing validation or reopen; no stale fallback or schema workaround.

## Reopen Triggers

Missing token/content relation fields, changed disabled/null/throw semantics, unbounded query growth, generation/secret issue, invalid shared lease. Invalidate integrated UI/performance and related cache evidence.

## Suggested Agent Prompt

After execution authorization, optimize only validated revision projection/materialization. Keep canonical and local validation outcomes, exact typed revisions, bounded races and dispatch secrets. Prove concrete loader improvement and malformed unchanged-token rejection before closing.
