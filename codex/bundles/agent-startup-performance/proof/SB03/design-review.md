# Independent SB03 design review

Decision: **Pass for the narrowed design below. Implementation and test gates remain open.** This review did not edit application code, run application tests, or change either live app. SB03 implementation remains dependent on the SB01 gate.

## Approved minimal change

Use an internal `ExistingRunDetailCommitOrigin` enum with `Prepared` and `RecoveredJournal`. The private store commit method receives this value explicitly: the immediate save call passes `Prepared`; the recovery call passes `RecoveredJournal`. This is ephemeral control flow, never serialized into the journal and never inferred from persisted data. Keep the journal schema, external APIs and recovery entry points unchanged.

Factor the four existing payload methods for session, run, execution index and aggregate usage projection through a private generic helper. Preserve its fresh `ReadJsonAsync` and the existing stored-versus-previous/target transition validation. After validation:

- For `Prepared`, when the freshly read typed payload differs from the target according to the existing payload comparison, call the existing `WriteJsonAtomicallyAsync` with the target. The target is already known to require a canonical write; the extra raw-text comparison read is unnecessary.
- Otherwise, call the existing `WriteJsonIfChangedAsync` unchanged. In particular, retain this path when the typed payload already matches the target and for every `RecoveredJournal` application.

The chat projection owner may make the same final write choice only after its complete existing expected-target rebuild and fresh current-index validation. Do not move chat projection logic into the execution-slice helper.

This targets up to five redundant raw comparison reads per ordinary changed progress commit: session, run, execution index, aggregate usage projection and chat index. It does not remove the five fresh conflict-validation reads. This is a prospective count reduction, not a measured latency result.

## Why the typed origin and narrow branch are appropriate

The origin is chosen by the trusted call site and expresses a real semantic boundary. It does not add a cache, mutable global state, a persisted trust marker, a service abstraction or a public API. The recovered-journal path retains its existing validation and idempotent write behavior.

A typed payload that differs from the target cannot have raw JSON exactly equal to the target's canonical serialization under the same serializer. Therefore the current code would write the target after its second read on the ordinary unchanged-filesystem path. The approved branch still uses the existing durable writer, including its current path/link validation, write-through/flush behavior, atomic replacement and observer behavior. It merely avoids reading the same file again to discover that already-known difference.

The helper is limited to the four aggregate/session/run payload kinds above. The chat owner handles its own index. A `ProviderUsageObservation` record must never enter this optimization: its history preparation/commit route remains untouched. Do not modify `FileSandboxWorkspaceJsonStore` globally or change record-directory diff behavior.

## Preserved safeguards and rejected alternatives

Keep both calls to `ValidateExistingRunDetailCommitJournal`, including the call after the journal has been durably written. `NormalizeRunDetail` copies outer collections but retains record objects and nested list references. An awaited journal write and the `JournalPersisted` callback separate those validations. They are not proven equivalent under mutable aliases.

Keep every fresh stored-state conflict read, including the current workspace index and the complete record-collection transition checks. The workspace lock coordinates participating writers; it does not make an earlier snapshot authoritative against unrelated edits.

Keep the complete chat expected-target rebuild. The latest-run resolver may read a different session/run when the preferred current run is not selected. Even the pure preferred-run case has not been proven safe to bypass in the presence of aliased inputs and the persisted journal contract.

Do not substitute prepared previous records for freshly loaded records in directory diffs. Most startup records are flat records and already compare equal after deserialization. Nested usage/checkpoint records are different: raw JSON can contain noncanonical formatting or unknown fields, while prepared previous and target instances compare equal. Reusing those instances can skip canonicalization that the existing materialized-record path performs.

Do not reuse a cached raw observation to skip a write when the typed/raw snapshot matches the target. A subsequent noncooperating edit can make that snapshot stale; the current comparison read would see and rewrite those bytes. Semantic equality also does not imply that original formatting/unknown JSON fields are already canonical. The approved design retains the old comparison path for this case.

## Risks and required proof

1. **Read reduction:** use the existing physical JSON read observer to show exactly one fewer comparison read for each changed supported payload in the prepared path, with no reduction in fresh validation reads. Cover a real append-progress commit containing a session/run and all aggregate/chat indexes. Retain the existing history-scale corpus; do not claim a startup improvement from a nested-record-only fixture.
2. **Canonicalization and no-op behavior:** compare original and candidate behavior for already canonical target bytes, semantically matching but differently formatted JSON, and additional unknown JSON properties. The matching typed-payload branch must retain the old raw comparison/write behavior. Unchanged collections are out of scope.
3. **Conflicts and path safety:** inject an unexpected stored run/session/index before its fresh read and verify explicit failure with the pending journal retained. Cover missing records and path/link swaps at existing safe test boundaries. The durable writer must retain its fresh safety checks before writing.
4. **Recovery and cancellation:** replay every existing commit fault boundary, corrupt/foreign journals, cancellation before journal persistence and cancellation after it. Recovery must use `RecoveredJournal`, preserve its old reads/writes, roll forward once, and retain all stage logs, continuation state and approvals.
5. **History and behavior:** demonstrate unchanged provider usage/history records and no duplicated logical stage or completion events. The aggregate usage index is distinct from a `ProviderUsageObservation`; test that distinction explicitly.
6. **Live acceptance:** after isolated gates and authorized app deployment, reuse the exact Phase0 diagnostic helper/filter and frozen agent/model/effort/context settings. Measure actual HTTP send, not the UI Run stage. Retain five fresh sessions and a separate continuation on each original host, genuine UI/tool behavior proof, and the agreed performance threshold.

There remains an existing race against a noncooperating writer after transition validation. If such a writer installs the exact target before the approved direct write, the candidate can perform an additional identical atomic write where the old comparison might skip it. Final canonical payload and logical commit stages remain equivalent; do not claim identical physical-write counts under that external race. If an existing contract/test requires that exact skip, keep the old path for that case rather than weakening the contract.

## Source evidence reviewed

- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs`: immediate journal validation/write/commit around lines972–1003; second validation and fresh workspace-index check around1043–1059; journal validation around1722–1780.
- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs`: current session/run writes around1053–1093; aggregate index writes around1179–1215; outer-only normalization around2677–2689; fresh collection validation and diff around2752–2846.
- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs`: record diff around235–286; raw-text comparison around289–301; existing observed/atomic writer around305–320.
- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceChatProjectionStore.cs`: complete expected-target/current-index validation around542–594; external session/run dependencies around646–662 and1314–1362.
- `tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests.cs`: all nine commit-stage fault cases, cancellation, corrupt pending journal, catalog mutation recovery and second-store blocking.
- `tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceAdmissionReadScalingIntegrationTests.cs`: existing physical JSON read observer and history-scale read assertions.

Line numbers describe the pre-SB03 source reviewed and can move during implementation.
