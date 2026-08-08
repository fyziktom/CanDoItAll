# Findings register

The evidence paths are relative to the reviewed repository at the baseline commit.

## FR-001 — Critical: Canonical execution authority is captured but not authoritative at runtime

**Claim:** The turn-capture pipeline resolves AgentExecutionAuthorityRecord and writes a projection to metadata, but capability planning, tool-provider context, workspace tool access, and invocation policy do not consume that authority as the single permission source.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentTurnContextCaptureService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentTurnContextMetadata.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`

**Why it matters:** MutationAllowed, ReadAllowed, AllowedOperations, allowed capabilities, and aliases can diverge from the actual tool graph. Product tools continue to re-derive permissions independently, leaving multiple authorization sources.

**Required action:** Create one immutable execution-governance snapshot from the admitted authority and make capability filtering plus invocation policy consume it. Remove duplicate permission derivation from tool providers where the canonical snapshot is sufficient.

**Target owner:** AgentFramework Core/Application + module authority providers

**Planned subbundle:** `SB01`

## FR-002 — High: Unknown source kinds can select workspace scope from UI-published state

**Claim:** CanonicalAgentExecutionAuthorityResolver has a canonical rule only for project-structure. Other source kinds retain ObservedWorkspaceScope and derive mutation compatibility from UiAccessHint instead of a durable source authority provider.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionAuthorityComposition.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentTurnContextCaptureService.cs`

**Why it matters:** A trusted server-side UI module with an incorrect or compromised publication can select another project-scoped workspace root. The comment that UI hints cannot grant authority is not enforced by the compatibility branch.

**Required action:** Introduce source-keyed canonical authority providers. Unknown sources must fail closed or receive a bounded read-only sandbox policy. UI hints may deny early but must never grant scope or mutation.

**Target owner:** Modules.AgentFramework authority composition

**Planned subbundle:** `SB02`

## FR-003 — High: Per-run workspace scope still leaks into recovery and script inspection

**Claim:** Normal tools use WorkspaceRuntimeServices created for the effective per-run scope, while finalizer recovery and script policy inspection retain the MafAgentRuntime construction scope.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafAgentExecutionAdapter.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafStreamingTurnExecutor.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafScriptPolicyInspectionService.cs`

**Why it matters:** A project turn hosted by an organization-scoped runtime can read or inspect a different managed path during recovery/policy evaluation than the path exposed to tools. This may cause false recovery, false denial, or cross-scope evidence use.

**Required action:** Pass the exact WorkspaceExecutionScope or WorkspaceRuntimeServices through every run-owned operation. Recovery readers and script inspectors must be created from that run bundle, never from runtime construction fields.

**Target owner:** MAF adapter workspace composition

**Planned subbundle:** `SB05`

## FR-004 — High: Workspace runtime bundle ownership and process-host lifetime are split

**Claim:** CanDoItAllAgentWorkspaceFactory constructs a direct LocalWorkspaceProcessHost and also creates WorkspaceRuntimeServices containing another process host. The workspace service does not own/dispose the bundle explicitly.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Runtime/WorkspaceRuntimeServices.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs`

**Why it matters:** Profile switches and workspace disposal can leave run-owned services or kept-alive processes outside the owner that performs cleanup. Two host registries can disagree about active process leases.

**Required action:** Create one owned workspace aggregate. Construct exactly one process host per workspace bundle, pass that same instance everywhere, and dispose the bundle once from the workspace owner. Prove handoff participant builds do not double-own it.

**Target owner:** Modules.AgentFramework workspace factory + Core workspace service

**Planned subbundle:** `SB06`

## FR-005 — High: Runtime-state restore checks the envelope wrapper instead of the inner MAF payload

**Claim:** ShouldRestoreSerializedSession calls SerializedSessionContainsProviderConversationId on SerializedSessionStateJson. New state is an envelope whose conversationId, when present, is inside PayloadJson.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/RuntimeStateEnvelopeModels.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`

**Why it matters:** Transient-context turns can restore a provider-managed conversation that the old safety rule intended not to restore. Existing tests use legacy raw session JSON and therefore miss the envelope path.

**Required action:** Evaluate compatibility first, unwrap only with the owning adapter, then inspect the inner MAF payload. Add envelope-based provider-conversation tests for ordinary sends and approval continuation.

**Target owner:** MAF session adapter

**Planned subbundle:** `SB08`

## FR-006 — High: State fingerprints conflate model context with authority policy and under-hash tools

**Claim:** ContextPolicyFingerprint is populated from ModelContextDigest. ToolsetFingerprint hashes only tool names. HistoryMode and AdapterPackageVersion are persisted but not considered by the compatibility policy.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafToolsetFingerprint.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeStateCompatibilityPolicy.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/RuntimeStateEnvelopeModels.cs`

**Why it matters:** A policy change can remain compatible when UI context is unchanged; a harmless UI fact change can invalidate state; a tool schema/classification/approval change with the same name can restore stale state.

**Required action:** Introduce separate authority-policy, model-context, capability-policy, and tool-contract fingerprints. Compare effective history mode and an explicit adapter compatibility range. Version and migrate the envelope.

**Target owner:** Core execution + MAF state adapter

**Planned subbundle:** `SB09`

## FR-007 — High: MAF tool governance still hardcodes policy and carries process-specific facts

**Claim:** MafRuntimeAgentFactory constructs DefaultAgentToolInvocationPolicy directly and builds ToolInvocationPolicyContext with ProcessRunId, ProcessStepId, product-mutation, branch, and process-operation fields from ambient audit state.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`

**Why it matters:** The public policy seam is bypassed, process semantics remain inside the MAF adapter, and ambient AsyncLocal data continues to influence authorization.

**Required action:** Inject a policy pipeline and pass a provider-neutral ExecutionGovernanceSnapshot. Processes contributes typed restrictions through a policy contributor; MAF maps tool calls but does not understand process fields. Keep ambient context telemetry-only.

**Target owner:** Core governance + Processes contributor + MAF adapter mapping

**Planned subbundle:** `SB12`

## FR-008 — Medium-High: Per-proposal approval exists in Core but the UI still applies one bool to all proposals

**Claim:** AgentChatPanel receives approved/rejected as one bool and expands it across every pending approval. Public API bool compatibility does the same.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `src/App/CanDoItAll.Web/Api/AgentApprovalDecisionRequestMapper.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentApprovalDecisionMismatchException.cs`

**Why it matters:** Users cannot approve a safe file write while rejecting a shell command in the same turn, despite the new application-owned per-proposal model.

**Required action:** Add per-proposal UI/API decisions, retain the bool endpoint only as a bounded compatibility mapper, and display exact tool/resource details without leaking raw sensitive arguments.

**Target owner:** AgentFramework UI + public API

**Planned subbundle:** `SB11`

## FR-009 — Medium-High: Approval and turn-context caches need bounded abandoned-run lifecycle

**Claim:** The MAF approval cache has no TTL/size bound. Turn-context leases protect WaitingOnTool entries from TTL eviction, so abandoned waiting runs can exhaust the bounded registry until explicit terminal cleanup occurs.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafApprovalContinuationDriver.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentTurnContextLeaseRegistry.cs`

**Why it matters:** Long-lived hosts can accumulate abandoned continuation state or hit the 64-entry turn-context limit. Restart behavior differs because typed attachments are in-memory only.

**Required action:** Define persisted-vs-ephemeral ownership, add bounded cache eviction keyed to durable run state, add an abandoned-waiting-run reconciliation job, and prove pending approvals remain fail-closed.

**Target owner:** Core execution lifecycle + MAF continuation adapter

**Planned subbundle:** `SB11`

## FR-010 — Medium-High: WorkspaceExecutionScope identity fields are designed but not populated

**Claim:** Production constructors generally pass only workspaceRoot and WorkspaceScopeDescriptor. Database profile, generation, authority identity/fingerprint, and execution run identity remain empty. Root equality is always case-insensitive.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Runtime/WorkspaceExecutionScope.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafAgentExecutionAdapter.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`

**Why it matters:** The same project GUID in two database profiles can share an identity projection, stale profile generations are not detected by the bundle, and Linux case-sensitive roots are compared with Windows semantics.

**Required action:** Build WorkspaceExecutionScope from execution run + admitted authority, include profile/generation/run identity, and use an OS-aware canonical path comparer.

**Target owner:** Core workspace runtime

**Planned subbundle:** `SB04`

## FR-011 — Medium: Lightweight LLM path is a good seam but not yet production-hardened for ordinary chat

**Claim:** The stateless port is provider-neutral, but contracts retain mutable arrays/lists, lack bounded attachment/deadline/correlation rules, propagate raw provider exceptions, and have no safe empty-response retry. Workflow DI remains in the MAF adapter project.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmInvocationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowLlmComponentInvoker.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProviderBackedLlmInvocationAdapterTests.cs`

**Why it matters:** Workflow LLM calls can expose provider exception text and fail on intermittent empty terminal responses. The current contracts are not yet a safe foundation for a user-facing ordinary LLM chat.

**Required action:** Add immutable bounded request contracts, typed sanitized failures, deadline/correlation, provider-neutral stateless retry, attachment validation, and move neutral workflow invocation/registration out of the MAF adapter.

**Target owner:** Llm.Abstractions + Llm.ProviderRuntime + Workflows runtime/hosting

**Planned subbundle:** `SB14`

## FR-012 — Medium: Runtime composition still contains hidden fallback construction and residual service location

**Claim:** MafAgentRuntimeDependencies.FromServices substitutes default implementations for several missing services, and ServiceProviderRegisteredCapabilityServiceSource resolves arbitrary configured service types at runtime.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntimeDependencies.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`

**Why it matters:** Tests and production hosts can silently use different graphs. Missing registrations are masked, and capability behavior depends on runtime service availability.

**Required action:** Fail fast for required production dependencies, model optional capabilities explicitly, and narrow the dynamic registered-service bridge to a catalog of approved descriptors.

**Target owner:** Hosting/composition + MAF dependency bundle

**Planned subbundle:** `SB16`

## FR-013 — Medium: Runtime.Abstractions still exposes transitional namespace and broad request shapes

**Claim:** Types physically moved to Runtime.Abstractions retain CanDoItAll.AgentFramework.Core namespaces. Requests still carry broad AgentDefinition/provider/session/capability/memory aggregates and SuppressApprovalRequirements.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/AgentRuntimeRequests.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/RuntimeResponseContracts.cs`

**Why it matters:** The port split is real, but ownership remains hard to read and approval semantics are still represented by a multi-meaning bool.

**Required action:** Align namespaces, replace ambiguous booleans with explicit policy records, and separate immutable execution blueprint from invocation-specific command data without destabilizing persisted models.

**Target owner:** Runtime.Abstractions

**Planned subbundle:** `SB16`

## FR-014 — Medium: Branch validation is documented but not independently reproducible from GitHub

**Claim:** The branch head has no GitHub commit status or workflow runs. Closure artifacts report builds/tests and also document known unit/integration failures plus a conflicting explicit-lease expectation.

**Evidence:**
- `codex/bundles/MAF-Refactor/CLOSURE-AUDIT.md`
- `codex/bundles/MAF-Refactor/architecture/15-final-state-summary.md`
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureRealMafPromptHarnessTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentRuntimeToolRoundTripIntegrationTests.cs`

**Why it matters:** The reported green state cannot be treated as an independent release signal, and known accepted failures can hide new regressions.

**Required action:** Re-run Release build, all test projects, architecture guards, CodeAnalytics dependency review, and deterministic live scenarios from the current head. Resolve the explicit-lease test conflict instead of extending an allow-list.

**Target owner:** Release gate

**Planned subbundle:** `SB00/SB17`
