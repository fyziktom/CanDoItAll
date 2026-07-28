# 04 Module Runtime Context Snapshot Adapters

## Status

- `Completed`
- Gate: `A4 Pass`

## Objective

- Capture immutable project-structure and process-workspace snapshots from already-held projections, attach the exact snapshot to the invocation, and reuse it for prompt/tool reads without duplicate deep persistence queries.

## Success Criteria

- Project Structure snapshot is produced by a pure held-surface adapter with independently typed monotonic publication revision, authorized content/selection fingerprint, coverage fingerprint, database-profile generation, freshness, and zero DbContext/file assembly calls.
- Bounded/redacted prompt text and the fuller authorized tool snapshot are derived from the same immutable publication and are never conflated.
- `project_structure_read` defaults to explicit `InvocationSnapshot` dispatch; covered reads use no storage, uncovered reads return `SnapshotCoverageMiss`, and only explicit `CanonicalCurrent` performs a deeper read.
- Snapshot attachments and their stamps are structurally unavailable to write paths; writes
  continue through current canonical application services and never replay captured UI state.
- Process snapshot uses typed present/absent vector components for every prompt/tool-visible source from Process Workspace and Live Processes rather than aggregate maximum freshness.
- Embedded Manager chat and floating context consume the same captured process snapshot.
- Concurrent UI updates yield exactly one complete old/new revision and never mutate an in-flight request.
- Multiple contributor-owned concrete attachment types round-trip opaquely through Core with no string key, `Dictionary<Type, object>`, or module reference.

## Covered Inputs

- R05-R06 and module half of R08.
- Runtime-first project tree/selection and selected process-run context.
- Snapshot atomic-publication/lifetime/source-of-truth policy; the existing registry lock remains the owner rather than adding an Interlocked-backed store.

## Prerequisites

- SB03 A3 gate passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Workbench\AgentTools\ProjectStructureAgentRuntimeToolProvider.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\AgentChat\ProcessAgentChatContextBuilder.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Projections`

## UI Composition Contract

- N/A; this subbundle changes backend/transient context contracts and no browser-visible composition.

## Deliverables

- Pure immutable project/process snapshot mappers, coverage descriptors, independently typed publication/content/coverage/profile/freshness records, and canonical expected-version records.
- Transient strongly typed invocation attachment carried by the existing `AgentChatContextRegistry`; no second registry or global snapshot cache.
- Context fragments built from the exact attached snapshot.
- Snapshot-backed project read path, explicit canonical-current read path, typed coverage misses,
  and structural separation from every mutation contract.

## Dependency Impact

- Backend measurement and UI truth depend on avoiding duplicate deep reads and associating activities with exact context revisions. A mixed or stale revision invalidates performance and semantic correctness.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical integration across Workbench, Processes, Agent Framework transient context, and runtime tools.

## Implementation Steps

1. Write pure-adapter tests that independently vary publication order, content/selection, coverage, profile generation, freshness, source/scope/type eligibility, concurrent capture, redaction, and storage-call count.
2. Add marker/kind/fingerprint/envelope contracts and extend each context-registry contributor registration with an immutable attachment list; enforce at most one concrete type per contributor.
3. Stamp envelopes with scope/source/workspace/contributor/kind identity; carry them through `AgentRuntimeTransientContext` and `AgentRuntimeToolProviderContext`; bind ordered identity, publication, content, coverage, profile, and freshness values into the transient-context digest and approval lease verification.
4. Retain/update an immutable Project Structure envelope after load/local edits; bind selection,
   coverage, and profile generation without presenting any projection stamp as a canonical
   mutation token.
5. Add `ProjectStructureReadSource`: dispatch `InvocationSnapshot` only for covered/current attachments, return typed unavailable/coverage-miss results otherwise, and perform storage only for explicit `CanonicalCurrent`.
6. Prove snapshot envelopes cannot flow into mutation contracts. Keep agent writes in the
   existing canonical application services, operating on current authorized entities, and
   refresh the UI projection only after the canonical commit.
7. Add a shared field-to-component map and process vector components for surface selection, shell refresh, definition catalog/selection, live-run summary/effective run, selected detail/record, runtime history/filter, focused event/files/agent, telemetry, and derived facts across both process surfaces.
8. Route floating and manager chat through the same captured process snapshot.
9. Capture A4 proof.

## Scope Exceptions

- Process-launch preview/execute snapshot drift and the incomplete `processContext` navigation handoff were discovered during exploration but are outside this chat-preload initiative.
- Deep project/process facts omitted from a snapshot may use only an explicitly selected canonical-current tool path during execution.
- Snapshot publication is an invocation read model, not a canonical mutation token;
  true row-version concurrency remains a separate domain-wide contract.

## Do Not Do

- Do not call the reload-based source-snapshot provider from the page.
- Do not use context-registry version as domain revision.
- Do not use publication revision or content fingerprint as canonical write concurrency.
- Do not use aggregate maximum process freshness as a coherent shell revision.
- Do not silently reload on coverage miss, unavailable snapshot, or stale mutation.
- Do not retain tracked EF entities/handles or serialize concrete attachments into run metadata.

## Acceptance Checklist

- [x] Project snapshot mapping performs zero persistence calls.
- [x] Publication revision, content/selection fingerprint, coverage fingerprint, and profile generation change independently according to their single meanings.
- [x] Expired/profile-mismatched attachments are explicit and never silently recaptured.
- [x] Exact source/scope/workspace/contributor/kind/concrete-type/profile/freshness/coverage eligibility is enforced; eligible dispatch performs zero persistence calls and explicit canonical-current dispatch is measured separately.
- [x] Invocation tool reads the exact captured snapshot during concurrent edits.
- [x] Prompt fragment is bounded/redacted while the authorized tool snapshot retains declared covered facts from the same fingerprint.
- [x] Snapshot payloads/stamps cannot be supplied to mutation methods, and no completion path
  writes captured projection state back to canonical storage.
- [x] Process revision vector covers every emitted field from both Process Workspace and Live Processes with typed present/absent components and a maintained field-to-component map.
- [x] Transient-context digest changes independently for publication, content, coverage, profile, or freshness identity changes and approval continuation rejects a mismatched lease.
- [x] Restricted paths/diagnostics remain redacted/authorized.
- [x] Two contributor-owned attachment types survive capture/invocation without object dictionaries, string keys, or Core module references.

## Proof Required

- Passing unit/component/integration tests, before/after storage-call counts,
  stale/concurrent semantic positive and adversarial negative evidence,
  `proof/SB04/manifest.md`, and `proof/SB04/a4-decision.md`.

## Browser Validation Logging

- N/A.

## Progression Gate

- A4 passed: exact invocation snapshot reuse, atomic old-or-new publication behavior,
  no write-back, redaction, typed provenance/freshness/coverage, and zero-I/O covered
  project reads are proven. A5 subsequently returned
  `GO with three P2 follow-ups`; those P2s do not authorize a hidden canonical-read
  fallback or snapshot write-back.

## Reopen Triggers

- Mixed revision, unauthorized detail, invocation reading newer UI state, or hidden fallback query reopens SB04-SB07.

## C# Architecture Contract

- Each module owns its concrete immutable snapshot/revision adapter.
- Agent Framework owns only the transient typed attachment boundary.
- Models define the empty marker/envelope; concrete immutable types remain in owning modules and Core transports them opaquely.
- Published attachments use immutable collections and the existing context-registry lock for atomic replacement/capture.
- Context/tool snapshot reads are read-only and coverage-governed; deeper canonical reads are explicit.
- Writes go through canonical services and cannot consume the invocation snapshot. True
  Project Structure row-version concurrency, if added, must be a separate domain-wide contract
  shared by UI and agent writers.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Implement exact runtime-first module snapshots and invocation reuse, prove concurrency/source-of-truth safety, update proof, and stop if A4 cannot pass.
```
