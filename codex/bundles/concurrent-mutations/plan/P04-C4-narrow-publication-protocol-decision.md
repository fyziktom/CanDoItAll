# C4 narrow publication exclusion: smallest conditional protocol

Architecture-only successor to writer-perimeter card 9FB56E2D…, work-accounting amendment 712B72AA… and the separately authored one-Fact small coarse probe D66784EA…. No production source or tests are authored by this note. Parent chooses the production strategy after the actual C3/D1 prerequisite and coarse cost evidence. The large two-row cost matrix remains prospective; its absence cannot be presented as qualification.

## Decision boundary

If the actual small forced-SQL-wait cohort shows unrelated same-organization checkpoints, cancellation or grants inheriting the publication wait, select exclusive keys for the original Agent, run and chat, while keeping the existing workspace lock only around file observation/publication. Do not build a generic lock service, cross-process reader/writer semaphore, rolling-upgrade coordinator or storage-format replacement. Same-Agent and shared-catalog changes may still wait; ordinary catalog read leases can also retain the coarse lock across their own work. This candidate removes the proposed C4 publisher's long organization hold. It does not claim all contention or fair scheduling is solved.

The change is larger than swapping the publisher's lock: every cooperating writer and every read-triggered recovery must participate before C4 can use the narrower exclusion. Keep activation disabled until the complete perimeter is implemented and proved. Provider/model work, approval waits and UI callbacks occur outside all publication exclusion. Actual C3 SQL necessarily occurs while the selected source/run keys are held.

## Closed keys and one neutral storage boundary

Retain the proposed `IAgentPlanningPublicationStore.AcquirePublicationSnapshotAsync(AgentPlanningPublicationExpectation, CancellationToken)` and immutable `IAgentPlanningPublicationSnapshotLease` from 9FB56E2D. Expectation data is the original `AgentToolSessionReference`, exact run/journal revisions and the owner-issued schema-six decision/generation/step/basis identities. It cannot confer authority. The actual Core planning owner first checks its live origin, stored policy/basis provenance and original ceiling, then the Workbench adapter uses the resulting claim with the existing C3 authority interface. No Workbench, EF or SQL type enters Models/Core/Persistence.

Only Persistence constructs the snapshot lease. It exposes the deeply frozen selected Agent, original run and chat identity plus acquisition-time catalog revision, and an active-lifetime check. A global catalog revision is **observation metadata**, not a final publication equality condition: a later permitted edit of unrelated Agent B must not invalidate held Agent A. Authority depends on the protected selected record and original run/decision, not an invented claim that the whole catalog remains unchanged. Shared capability/provider/team changes are enrolled conservatively as all-Agent changes below.

Add one private closed key value in Persistence: `AgentSource(Guid)`, `ExecutionRun(Guid)` or `ChatSession(Guid)`. Derive keys from validated prior/target owner records, never caller-supplied names. The total order is fixed kind then Guid N ordinal, identical in every entry. Use the existing `DurableFileWriter.AcquireCoordinationAsync` and path/link rules; no new Infrastructure strategy. Paths are under a stable coordination child of the canonical scoped data root, outside the Agent-deletion run/session directories. Distinct store instances share the exact physical domain. Closing a handle releases ownership; do not delete lock paths during ordinary Agent/run cleanup.

The held order is:

```text
existing dispatch lease, if already required by the actual invocation
  → complete sorted source/run/chat keys
  → short instance gate + workspace.lock
  → release gate/workspace.lock
  → C3 epoch/operation/Project SQL locks, flush, commit, transaction disposal
  → release source/run/chat keys
  → outcome journal publication / authorized receipt disclosure
```

The publisher must reuse its actual already-held dispatch capability rather than reacquire its own nonreentrant dispatch file. Non-dispatch cancellation never acquires a dispatch lease later. No code waits for target keys while holding workspace.lock, nor reenters ordinary file readers from inside C3's Project transaction.

## Owner write sets and conditional publication

Known single-run mutations derive the run and original/proposed chat keys from validated saved state. A changed Agent/chat association either adds its complete original/target keys before acquisition or fails the existing identity rule. Agent publication also holds the selected Agent-source key. Catalog writers derive changed Agent-source keys from the full normalized prior/target records; changing shared capabilities, providers, teams or otherwise unclassified catalog material conservatively includes the union of all prior/target Agents. Normalization and `AgentProjectAccessCatalogPolicy` changes count. New/deleted IDs, full replacement, import and all-Agent revocation cannot be omitted because they were not an editor's requested ID.

The existing update contracts take synchronous `Func` callbacks. Preserve exactly one callback invocation; do not replace them with retries or a new command framework. Their execution needs three owner phases:

1. **Observe and prepare.** Enter the short workspace guard only after pending recovery has been handled by the recovery procedure below. Read the owner source/index witnesses. Execute the callback once, run existing structural validation/normalization and capture the closed affected-key set. Freeze all retained mutable collections and exact prior/target data in a private owner-created prepared value. Caller-owned aliases cannot mutate it while the owner awaits locks. Returning the original reference remains a validated no-op only within this guarded observation; no prepared value is an accepted result.
2. **Acquire and compare.** Release workspace/gate, acquire the complete sorted keys, reacquire workspace/gate, and compare the exact original physical/domain witnesses. Any run/chat/catalog/index or pending-journal change invalidating the prepared operation refuses. No newly discovered key is appended while a subset is held. Do not invoke the user callback again. A callback's outer result/receipt locals are never returned as accepted after this failure.
3. **Publish.** The final catalog policy may confirm the exact prepared target and key set using current facts or refuse; it cannot silently add a changed Agent. Only now invoke the existing durable writer/journal protocol. Keep its noncancelable completion after the durable journal publication boundary and all existing acknowledged/uncertain-result handling. Dispose keys after the owner publication/disposal ends.

For an actual workspace-index revision mismatch, reuse `SandboxWorkspaceConcurrencyException` with the real expected/actual revisions. Its current message explicitly asserts a revision mismatch; do not construct it with equal values for a physical/pending-journal conflict. For those cases, add only one closed owner-specific conflict reason/exception at the existing Core workspace concurrency boundary (for example OriginalBytesChanged, PendingRecoveryChanged, WriteSetChanged). It means the **new prepared operation** was not published and requires a fresh explicit caller decision; it is not a transient automatic retry signal. Existing already-durable recovery may have completed before that refusal, and must be reported separately where an existing result carrier requires it.

This is a deliberate contention-compatibility change for previously unconditional `Save`/`Update` methods: a plan can now fail between callback and publication. Current callbacks can set outer locals, so caller exception/result mapping is part of the release. A synchronous callback is not by itself proof of purity; any discovered external mutation in a current callback is a release blocker until its exact semantics are preserved. Do not silently promise rollback of arbitrary callback side effects. The only intended new outward effect begins at the owner's existing durable boundary.

## Complete lower writer perimeter

The exact source inventory in 9FB56E2D remains the perimeter; the following partition determines the minimal implementation rather than adding ad hoc locks to callers.

| Existing owner | Required closed state |
| --- | --- |
| `SaveExecutionRunDetailAsync`, both atomic run-update overloads | Exact run plus prior/target chat and any validated identity dependencies. Covers all D1 acquire/decision, claim, checkpoint, cancel and late accepted/uncertain outcome writes, and ordinary approval/provider/checkpoint/reconciliation writes. |
| Chat-backed start, generic/background reservation, chat create/update/save | Original/target run/chat identities and original selected Agent when admission reads its mutable definition. No same-chat start bypass. |
| Catalog save/update/normalize | Changed Agent-source keys; all-Agent union for shared or unclassified catalog change; policy-added changes included. |
| Full execution/workspace save/update, import, repair | Union of affected prior/target runs/chats/Agents, including removals. Freeze original membership so concurrent new membership cannot escape a broad plan. |
| Agent deletion | Selected Agent plus every actual removed run/chat; use its prepared execution plan and original/target catalog. |
| All four semantic recovery journals | Derive exact keys from the validated durable prior/target data, including Agent-deletion catalog and membership. Never infer empty write set from a failed decode. |
| Read-triggered migration/seeding/normalization | Treat as prepared broad mutation or explicit unsupported conflict, not a read-only escape hatch. |

Interview and usage-index maintenance writers that cannot change the protected Agent/run/chat source remain under their existing workspace coordination and need no new planning key solely for their independent files. They still matter as coarse-lock wait controls. Historical receipt review remains strictly read-only and gains neither publication keys nor mutation permission.

## Recovery without lock inversion or callback replay

The current ordinary readers enter `RecoverPendingExecutionCommitAsync` while holding workspace.lock. That is the principal required refactor: **do not** add target-key waits inside it.

Inspect the exact existing journal under the short guard and validate its complete shape using the current validators. Produce a private recovery witness containing exact journal bytes/digest, required prior/target record identities and the closed key set; no client-created witness or mtime/length cache. Release workspace/gate, acquire sorted keys, reacquire workspace/gate, and verify the same pending journal plus its existing allowed previous/target states. Complete only that observed journal using existing durable semantics. A changed/different journal or an additional required key refuses explicitly; no recursive recovery under a held subset and no unbounded loop hunting a new plan.

If a publication acquisition discovers a pending journal after obtaining its keys, it releases the subset and returns a typed currentness/recovery refusal. It does not recover a different run while holding an unrelated source key. The caller may later explicitly retry the same original intent after current-state reconciliation; no provider/effect invocation is inferred. On ordinary read paths, a recovery conflict is an error, never Missing or an empty list. All four retained journal formats remain readable and use their original noncancelable completion boundary; this proposal does not rewrite their payloads or erase previous/target validation.

## Minimal implementation order if selected

1. Add the private key/path/held-handle mechanics and explicit conflict result at the existing Core concurrency seam. No publication consumer is enabled. Prove separate instances, partial acquisition, cancellation, identity/path protection and no recursive workspace wait.
2. Enroll all write entries and read-triggered recovery as one coherent owner protocol. `FileSandboxWorkspaceStore.cs`, `.AgentDeletion.cs`, `.BackgroundReservation.cs`, `.CatalogReadLease.cs` only where read-triggered preparation is necessary, and `FileSandboxWorkspaceStorageLayout.cs` are the existing owner edit set. One new owner-specific file can hold the cohesive key/prepared-witness logic; it must not become a generic callback engine. Execution-slice payloads, JSON serialization and Infrastructure durable publication remain unchanged unless a concrete dependency is separately reviewed.
3. Only after full perimeter proof, expose the neutral publication snapshot contract and wire the actual Core decision owner plus one Workbench authority adapter. Original source/step/policy/active-work and schema-six/C3-v2 gates remain separate; do not use a test authority as their implementation.

The private plan families can reuse current run/chat/deletion journal records. Full catalog/workspace plans still need immutable prior/target state, membership and physical witnesses; do not preemptively unify these into an extensible transaction framework. Their allocation and broad-key costs are measured requirements. There is no present whole-catalog/run maximum justifying a new arbitrary Agent/key/byte cap.

## Supported cutover and failure proof

Supported installation is offline/quiesced and homogeneous: stop all application, tool and background writers; verify exact owner-process/file/SQL cleanup; complete or explicitly refuse pending recovery with the current owner; install the one reviewed writer protocol in every permitted host before activating publication. Existing schema refusal remains in place. Old binaries ignore new key files, so concurrent old/new writers are unsupported; a marker they ignore is not enforcement. Downgrade likewise requires quiescence and supported data/schema compatibility, otherwise it is refused. No rolling-upgrade layer is requested.

Required prospective proof, not newly authored cases:

- Same original run: live checkpoint, approval, D1 cancellation before/after entered owner commit, late accepted/uncertain outcome, both atomic overloads and raw detail replacement. Exactly one legal terminal knowledge path, no reopened decision or duplicate operation.
- Same source: grant/revoke/delete, shared catalog edit, full import/repair, normalization-added binding and concurrent membership. Unrelated B can proceed during A's actual SQL wait; a global observed catalog revision must not accidentally serialize/reject that supported independent progress.
- Callback executes once; final conflict exposes no prepared acknowledgement. Caller-owned collections mutated during lock wait cannot change the plan. Final policy discovering another key refuses without writing.
- Each of the four recovery formats after real interruption, reached from both a writer and ordinary read, with exact previous/target validation, different-journal race and complete key release. No workspace-held wait on a target key.
- Partial lock acquisition/cancellation, release failure and process crash preserve known commit/uncertainty; fresh processes reacquire only after actual owner exit. No fairness claim from one cohort.
- Homogeneous cutover and refusal of unsupported schema/rollback paths; no claim of safe simultaneous old writers. Parent owns runtime/process evidence.

The first authored cost Fact is deliberately only a decisive small coarse-control probe. A later narrow candidate must run the same matched cohort and preserve successful writer effects/cleanup, then obtain the agreed normal/max-file and sustained-arrival evidence before user-facing activation. No result of this architecture note closes the requested persisted settings or ordinary Agent end-to-end feature.
