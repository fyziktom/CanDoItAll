# Current State

## Rollback State

- The prior four-file change that pushed .NET validation receipt behavior into generic process completion handling was rolled back before this bundle was prepared.
- `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore --configuration Debug` succeeded after rollback with only the existing `NU1903` `Microsoft.OpenApi` warning.
- The local 5032 instance was restarted from the reverted source and returned HTTP 200.
- The latest inspected run `b5b2e2df-f952-4fb9-913d-3cb22f9f231e` was launched before rollback, so its persisted assignments still contain the reverted bad patch's extra receipt gates in some child steps. Treat that run as diagnostic evidence, not as proof of current source behavior.

## Latest Run Shape

- Root run: `b5b2e2df-f952-4fb9-913d-3cb22f9f231e`.
- Root status: `NeedsAttention` through the API, `Blocked` in `process_runtime_states`.
- Completed child run: `e7b34744-59bd-4932-9797-0ac2265f5d8e`, architecture design/review.
- Blocked child run: `8ee277bd-ab2b-4d82-93df-4301737f95ae`, .NET solution setup, blocked at `create-dotnet-project`.
- Blocked child run: `cddba584-2ed7-4a5d-91a4-82b6059ff7c1`, .NET implementation slice, blocked at `prepare-solution-skeleton`.
- Root `implementation` blocked because the implementation child run blocked.

## What Could Be Observed

- `process_runtime_steps` and assignments show which steps are blocked, pending, completed, and which launch variables/prompts were assigned.
- `process_strategy_result_receipts` records outcome, applied status, result hash, and idempotency data.
- `process_projection_history` records generic `StepBlocked` and `ProcessRunBlocked` events.
- Step prompts contain detailed process/domain instructions, including .NET scaffold plans, managed artifact refs, and tool receipt expectations.
- `ProcessRuntimeDispatchApplicationService` contains automatic retry and manager-result suppression logic around `StrategyResultEnvelope`.
- `ProcessCapabilityScope` can carry directives, instruction fragments, and required tool receipts.
- MAF runtime capability access policies can deny tools based on workspace settings, browser access, allowed operations, process-step operation requirements, and scope override policies.

## What Could Not Be Observed

- Blocked projection payloads did not include the actionable reason, diagnostic codes, failed required tool name, MCP name, policy denial, or strategy result details.
- `process_strategy_result_receipts` persisted hashes and statuses but did not expose the full `StrategyResultEnvelope` reason/diagnostic payload needed for root-cause classification.
- `process_artifact_ledger_events` held slot id, artifact id, and content hash, but no easily discoverable artifact path or readable content lineage from the API/read model.
- `AgentFramework_WorkflowRuns`, `AgentFramework_WorkflowEvents`, and `AgentFramework_WorkflowArtifacts` did not contain useful rows for the inspected blocked window.
- Managed artifact files for the inspected run were not discoverable through the usual top-level `artifacts/process-runs/<runId>` path after restart.

## Current Architecture Observations

- `ProcessRuntimeDispatchApplicationService` is large and mixes scheduling, claim cleanup, strategy dispatch, retry prompt mutation, repeated retry suppression, branch result handling, and diagnostics aggregation.
- `ProcessRuntimeProjectionQueryService` is large and mixes projection reads, workspace aggregation, active-agent enrichment, operator action enrichment, and step/run metadata enrichment.
- `ProcessRuntimeEvidenceSourceProvider` is large and mixes runtime state, assignments, receipts, artifact ledger, projection history, dead letters, agent sessions, and memory-source projection.
- `AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs` is a sensitive boundary: it currently validates product completion and blocker text. It must not accumulate .NET-specific validation semantics.
- `WorkspaceImageAnalysisPromptNormalizer` is currently generic and avoids assuming UI/software-development intent. Keep it that way.
- `ProcessCapabilityScopeModels.cs` has a starting model for directives, instruction fragments, and required receipts, but it is not yet a full readiness contract for tools, MCPs, skills, allowed operations, and suppressions.
- `RuntimeCapabilityComposer.Access.Policies.cs` enforces some runtime policies after context assembly, but launch/readiness does not yet provide enough user-facing diagnosis when a step's required tools or MCPs cannot be injected.

## Current Template Observations

- `software-delivery`, `dotnet-solution-setup`, `dotnet-development-slice`, and `dotnet-ui-screenshot-writeback` carry extensive domain instructions through JSON and step markdown.
- .NET delivery templates correctly belong outside generic runtime, but their current rules are long, string-heavy, and hard to unit test as driver-owned behavior.
- Existing tests use Calculator and Tetris as fixtures, but those examples should remain fixtures only. They must not become implicit generic process assumptions.
- Browser/screenshot proof requirements belong to UI-visible validation steps or visual automation drivers, not to all development or management steps.

## Preliminary Root-Cause Split

1. Diagnostics lineage gap: the process can block but does not expose enough typed facts to explain why.
2. Capability readiness gap: required tool/MCP/skill/suppression needs are not validated as a coherent step contract before dispatch.
3. Driver recovery gap: manager fallback does not classify missing artifact, denied tool, missing MCP, instruction mismatch, provider failure, timeout, or child-run blocker with driver-owned recovery policy.
4. Domain isolation gap: .NET/product completion rules are too easy to add to generic adapter/runtime code.
5. Template hardening gap: .NET process prompts are doing too much orchestration by prose instead of invoking smaller driver/testable policy components.
6. Regression proof gap: current tests prove selected fixture paths but do not replay the actual escalation classes with enough negative cases.
