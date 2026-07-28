# C# Architecture Governor Review

## Verdict

Proceed with a minimal adapter-local migration. The existing architectural isolation is correct. The upgrade must not introduce a new façade, duplicate approval model, provider-specific options branch, or native-workflow persistence subsystem.

Evidence is based on CodeAnalytics snapshots `snap-20260728042508-0d7f96ce` and
`snap-20260728054006-aa62cd27`, direct source review, and the final architecture
skeptic pass.

## Responsibility Map

| Concern | Existing owner | Upgrade decision |
|---|---|---|
| Stable and preview package train | Three MAF project files | Use two shared MSBuild version properties; add no project reference |
| Shared MAF agent options | `MafRuntimeAgentFactory` | Preserve 1.13 mixed-tool parity once at the common construction point |
| Provider-specific client creation | Sole `IMafProviderAgentFactory` implementation | Keep unchanged unless compilation proves an API break |
| Approval request reconstruction | `MafApprovalContinuationDriver` | Retain exact restored session and stable persisted IDs; fail closed when no stable ID exists |
| Streaming tool-call snapshots | MAF provider streaming adapter | Never manufacture an approval-capable identity |
| Approval decisions and stale-state admission | Existing Core models and state transitions | Reuse; do not add a second DTO or persistence path |
| Session serialization and attachment scrubbing | MAF session persistence driver and request-scoped scrubber | Characterize actual 1.15 state; change only on a failing test |
| Handoff terminal projection | MAF workflow adapter and production streaming runtime | Characterize direct and streaming paths before changing the projection owner |
| Execution checkpoint payload | Application-owned checkpoint bridge payload | Preserve; test the MAF index seam only |
| UI orchestration | Existing Blazor modules and application services | No SDK types and no UI changes unless a real behavioral gap is proven |

## Selected Patterns

- Adapter boundary: keep all MAF types behind the existing runtime and workflow adapter projects.
- Composition root: configure `ChatClientAgentOptions` at the single common factory seam used by OpenAI, Azure OpenAI, Ollama, and handoff participants.
- Fail-closed validation: reject an approval-bearing request that has neither a stable request ID nor a stable tool-call ID.
- Characterization before adaptation: add deterministic 1.15 tests around serialized approval state, handoff output, and checkpoint index compatibility; modify runtime behavior only when a test proves the mismatch.
- State snapshot rollback: drain or reissue legacy approvals and restore the pre-cutover store snapshot on rollback.

## Explicitly Rejected

- A new decision contract: `PendingToolApprovalDecision` already exists.
- A full per-ID UI/API migration solely for the package upgrade: current continuation is atomically bound to the complete server-held approval snapshot.
- A C1-C6 private-JSON classifier: private framework state is not an application compatibility contract.
- A replay bridge that reconstructs a 1.13 approval into 1.15 executable state.
- Provider-by-provider option duplication: every supported provider receives the shared options object.
- A broad native workflow checkpoint migration: the bridge persists an application-owned payload, not a native workflow request envelope.
- Reflection-based terminal-output handling without a supported 1.15 signal.
- Splitting large MAF classes as incidental upgrade work.

## Dependency and Partial-Class Gates

- Allowed dependency direction remains `Modules/UI -> Core contracts -> Models`.
- MAF adapters may depend on Core, Models, and the workflow adapter.
- Core, Models, and UI must not reference Microsoft Agent Framework SDK types.
- No new project reference is planned.
- No new partial class is planned.
- Existing large classes may receive a narrowly scoped edit at their current responsibility seam; structural extraction requires separate evidence and review.

## Invariants

- Approval response IDs are stable and owned by the current persisted approval snapshot.
- Missing or incompatible session state with pending approvals fails predictably.
- Approval binding remains enabled.
- Mixed calls retain 1.13 parity during the upgrade.
- Pending approval state is consumed at most once.
- Attachment scrubbing cannot remove framework approval binding state.
- Handoff depth limits and terminal output do not cause duplicate execution.
- Custom file tools and their path, alias, script, approval, and concurrency policies remain authoritative.
- Runtime provider resolution remains invocation-scoped; no mutable singleton captures an agent runtime.

## Handoff Depth-Guard Boundary

The production `MafAgentRuntime` consumes the handoff agent through
`RunStreamingAsync`; that path observes each handoff call before yielding the
corresponding update and rejects an over-depth transition without projecting it
as terminal output.

The SDK's direct non-streaming `RunAsync` contract returns only after the inner
workflow has completed. The wrapper therefore delegates directly to preserve the
complete 1.15 response and performs depth observation as a post-run diagnostic.
It must not be described as preventing mutations performed inside an already
completed direct run. Direct-path tests are characterization and response-
transparency proof; production depth enforcement is owned by the streaming
execution path. Moving enforcement earlier would require a separately reviewed
workflow-transition boundary change and is outside this package upgrade.

## Testability Contract

Tests must target observable seams:

- exact package and loaded-assembly versions;
- the common options object;
- stable approval ID mapping and fail-closed behavior;
- serialize, scrub, restore, and respond-once behavior;
- direct and production-streaming handoff projections;
- 1.13 checkpoint index read under 1.15;
- existing architecture boundary tests;
- UI entry surfaces through their actual application service paths.

No test-only production API or SDK type may be added merely to expose internals.

## Stop Conditions

- A claimed 1.15 API is absent from restored assemblies.
- Scrubbed 1.15 approval state cannot be deserialized with binding intact.
- A pending approval lacks a stable request or call ID.
- A runtime change would require inspecting or rewriting private framework JSON.
- A handoff fix would require unsupported reflection.
- An architecture test detects SDK leakage or a new dependency cycle.
