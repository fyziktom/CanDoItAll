# D1 guarded transfer readiness after the C3 checkpoint

Read-only source comparison against main HEAD `702d0a93b1081c42d5a06893e7070219aee5d089`. Main has uncommitted integrated work; HEAD alone is not its identity. The companion JSON records exact observed source bytes and hashes. This is transfer preparation, not permission to change the currently frozen main checkout, a compilation result, or execution proof.

## Exact transfer

Frozen source inventory `R1d-D1-dormant-source-inventory.json` remains SHA256 `695F5E78186BC56F93867174209B56487A4A34A52BDB86A0A10E4FD81BCB2D07`. All twelve listed managed inputs still match it. All five saved predecessor images still match their recorded hashes. Current main has those exact five predecessors; the seven new destination paths are absent. No merge or additional source overlay is needed at this observation.

Transfer only those twelve inputs after the parent releases main: nine production files, two Integration files and the original source proof. The original inventory can accompany them unchanged as proof. The five replacement guards are the inventory's existing `MainBaseSha256` values; the other seven paths require absence. Recheck every source hash, predecessor/absence guard and the companion read-only dependency guards immediately before any copy. Stop on mismatch rather than resolving it by taking a whole directory or the latest managed file.

The replacements are `AgentToolRunLease.cs`, `AgentToolAdmissionJournal.cs`, `AgentToolReceiptReconciliation.cs`, `AgentRuntimePorts.cs` and `IAgentRuntimeToolProvider.cs`. These are actual owner/ABI successors, not independent additions: transfer them together with the four new production files. The inventory and companion JSON retain their full relative paths and full predecessor/successor hashes.

## Existing integration and exclusions

The thirteen directly inspected owner/model/store/fixture/project dependencies are identical between current main and managed. In particular, `AgentToolAdmissionJournal.Effects.cs`, `AgentToolAdmissionJournal.ProviderDispatch.cs`, approval cancellation, execution transitions, `AgentToolJournalModels.cs`, the actual file-store owner and `AgentToolAdmissionJournalFixture.cs` already provide the required seams. `AgentToolJournalTransitions.cs` remains exactly `5AD46EEB72D9B9B44FD544657C968BF4E86CE977DBBA212999DD4B036CDE5186`; it is a guard, not a transfer input. Existing SDK-style project inclusion and project references cover the new Models, Runtime.Abstractions, Core, Tooling and Integration files. No project-reference or composition registration edit is needed.

The current historical source and C3 source are not replay prerequisites or overlays. Their integrated versions must remain in place, including the historical seed-host project reference and C3 codec repair. D1 adds optional unsupported interface defaults and no registration or producer activation. Do not copy historical archives, old C3 sources, whole managed project files, the Resource work, the C4 probe or its `ProjectTaskPlanOperationFixture.cs` successor. The latter deliberately differs from main's preserved C3 fixture and is unrelated to D1. No migration, database schema, model snapshot, activation descriptor, renderer register or static baseline belongs to this transfer.

## Six-row source selection and release order

The actual D1 test source contains exactly six `[Fact]` methods, with no theories, data providers, skips or opt-in traits. Their names match the frozen inventory. The class carries `Category=FileSystemPortability`; the exact selector is `FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.AgentSavedEffectDispatchTests`. Expected discovery and execution are both six. Receipt observations in these file-owner cases are explicitly labelled doubles; this is not actual provider dispatch or PostgreSQL receipt proof.

1. Parent finishes and releases the current frozen C3 attempt, retaining its result and cleanup evidence.
2. Recheck the twelve guarded inputs and dependency observations, then perform this cohesive transfer only.
3. Build the affected Models, Runtime.Abstractions, Core and Tooling projects and owning Integration project in standard Release with serialized shared outputs. A normal owning Integration build supplies transitive references; never reuse stale dependencies through `--no-dependencies` without an independently complete build set.
4. Complete the repository static procedure with untracked protected files, reviewed baseline deltas and final no-write enforcement. Parent declares the existing origin/admission/receipt/activity regression selection independently; this note does not invent additional passing counts.
5. Build-backed exact discovery must produce the six declared methods before executing them against the same frozen sources/binaries. Preserve failure and cleanup evidence.
6. Only after the parent reviews the D1 six-case runtime result may the separately inventoried one-row C4 coarse probe be integrated/executed. C4 production, D2 provider dispatch and public/native activation remain held.

No build, discovery, test, scan, database command or host was invoked for this comparison. Only this new note and its companion review manifest were authored; frozen D1/historical/C3 inputs and main remain unchanged.
