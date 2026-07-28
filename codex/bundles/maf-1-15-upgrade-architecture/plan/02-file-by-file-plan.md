# File-by-File Change Plan

Paths are based on the pinned branch. SB01 must adjust this list for repository drift and discovered files.

## Shared package versioning

### `src/MAF/MicrosoftAgentFramework.Packages.props`

Planned:

- own `MicrosoftAgentsAIStableVersion`;
- own `MicrosoftAgentsAIPreviewVersion`;
- import the file only from the three direct MAF package owners;
- do not enable Central Package Management;
- leave repository-wide compiler/default-item settings unchanged.

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

Planned:

- stable core/OpenAI/workflows references use the stable property;
- A2A uses the preview property;
- do not change MEAI/OpenAI/Azure/MCP/Ollama versions unless restore proves a conflict;
- audit `NoWarn`.

### `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`

Planned:

- core/workflows use stable property;
- audit broad MAF warning suppressions.

### `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`

Planned:

- Hosting.A2A uses preview property;
- inspect any transitive Hosting package version.

## Agent construction and approval defaults

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`

Planned:

- explicitly preserve 1.13 mixed approval behavior in parity phase with `DisableApprovalNotRequiredFunctionBypassing = true`;
- explicitly document/ensure `DisableApprovalResponseBinding = false`;
- verify `UseProvidedChatClientAsIs` and provider factory behavior;
- preserve application invocation policy and runtime build ownership;
- inspect builder/middleware order;
- later feature-gate 1.15 bypass adoption.

### Every `IMafProviderAgentFactory` implementation

Discovery-owned path list.

Planned:

- prove options are not discarded;
- prove default middleware is not bypassed;
- if a custom stack is used, explicitly add binding as outermost and preserve per-service-call persistence;
- record effective middleware order in tests.

## Approval persistence

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafApprovalContinuationDriver.cs`

Planned:

- remove random approval ID fallback;
- require stable request and call IDs for every pending request;
- require the complete current server-held pending snapshot before admitting a
  decision;
- make the exact serialized MAF session plus application pending snapshot the
  atomic persistent authority and the cache an optimization;
- support native 1.15 continuation;
- reject continuation without native 1.15 binding state and drain or reissue it;
- consume the restored native binding state at most once;
- preserve function and MCP shapes;
- add structured diagnostics.

### Pending approval model and persistence files discovered in SB01

Planned:

- persist the exact serialized MAF session and pending snapshot atomically;
- preserve stable request and call IDs;
- ensure at-most-once consumption;
- preserve redaction;
- represent incompatible/pre-1.15 state as a typed drain/reissue outcome without
  inspecting private MAF JSON.

### UI/API approval endpoints discovered in SB01

Planned:

- display exact server-held tool call and arguments;
- submit against the complete current server-held pending snapshot;
- distinguish reissued/legacy/incompatible outcomes;
- reject a decision if the server-held snapshot has changed.

## Session creation and persistence

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`

Planned:

- preserve governed-step isolation;
- preserve provider/framework history rules;
- reject approval continuation when no native 1.15 serialized session state is
  available;
- remove or implement dead `ShouldReplayTranscriptAfterApproval`;
- add fixture-driven handling for 1.13 serialized state;
- consider replacing raw JSON sniff only after parity.

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionPersistenceDriver.cs`

Planned:

- keep bounded timeout;
- return/emit typed failure classification instead of silent catch-all;
- preserve request-scoped attachment scrubber;
- prove `_pendingApprovalRequests` and other state-bag values survive scrubbing;
- persist the session and application pending snapshot within one consistency
  boundary.

### Request-scoped session scrubber source discovered in SB01

Planned:

- add tests for arbitrary state-bag entries;
- remove only attachment payloads;
- do not strip tool arguments or approval state;
- prove no raw bytes/base64 remain.

## Handoff and response projection

### `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafHandoffWorkflowFactory.cs`

Planned:

- add characterization tests around builder events and terminal outputs;
- change `HandoffDepthGuardAgent.RunCoreAsync` so it no longer blindly reconstructs all updates as final output;
- move/enforce depth guard at a transition/event boundary if possible;
- preserve cancellation/disposal;
- expose or attach a supported terminal projection descriptor for the streaming runtime if needed;
- avoid reflection and opaque RawRepresentation parsing as permanent design.

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`

Planned:

- distinguish activity updates from authoritative result projection for workflow agents;
- keep ordinary-agent streaming behavior;
- ensure approvals and finalizers are extracted from authoritative response;
- preserve progress callbacks and background continuation;
- avoid duplicate tool execution.

### Response snapshot/streaming runner files discovered in SB01

Mandatory targets:

- `MafAgentResponseSnapshotter`;
- provider streaming runner;
- any update merger or message normalizer.

Planned:

- document every transform;
- remove timestamp sorting/synthetic grouping only after fixtures;
- preserve raw event metadata needed for workflow projection;
- ensure binding runs through `AIAgent` with active session.

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeResponseAssembler.cs`

Planned:

- keep required-finalizer validation;
- validate usage grouping against MEAI 10.8;
- ensure empty-completion detection examines authoritative response;
- compare finalizer repair triggers before/after workflow projection fix.

## Checkpointing

### `WorkflowBackedAgentExecutionCheckpointBridge` and native workflow stores discovered in SB01

Planned:

- trace whether native MAF checkpoint/external request envelopes are stored;
- capture 1.13 fixture;
- resume under 1.15;
- remove assembly-identity workaround only if proven redundant;
- preserve application checkpoint governance.

## File tools and hosting

### `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`

Planned:

- no lifecycle regression;
- preserve singleton/scoped registrations;
- verify A2A registration;
- verify no Harness file provider is added.

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeDependencyResolver.cs`

Planned:

- no architecture replacement;
- regression tests for fallback and DI-provided services;
- prove workspace scope remains run-correct.

### File/capability/tool projects discovered in SB01

Planned:

- no MAF-driven rewrite;
- add regression tests and tool inventory snapshots;
- check duplicate names after package update.

### A2A hosting/mapping files discovered in SB01

Planned:

- compile API changes;
- smoke test agent card, message, streaming, session, error redaction;
- preserve endpoint authorization.

## Tests

Use existing test projects where ownership already exists. Add focused test classes named approximately:

- `Maf115PackageAlignmentTests`
- `Maf115ApprovalBindingCompatibilityTests`
- `Maf115ApprovalContinuationSecurityTests`
- `Maf115MixedToolApprovalBehaviorTests`
- `Maf115HandoffResponseSemanticsTests`
- `Maf115ResponseMergeRegressionTests`
- `Maf115SessionCompatibilityTests`
- `Maf115WorkflowCheckpointCompatibilityTests`
- `Maf115FileToolRegressionTests`
- `Maf115RuntimeIsolationTests`
- `Maf115A2AHostingSmokeTests`
- `Maf115RollbackCompatibilityTests`

Names and placement may follow existing conventions, but coverage may not be reduced.
