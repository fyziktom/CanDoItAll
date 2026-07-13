# 17 Memory Workflow Executor And Template Integration

## Status

- `Completed`

## Objective

- Add generic memory workflow executor, operation settings, provider selection, template updates, and compatibility mapping from old native workflow executor ids where needed.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R09
- R10
- R11

## Prerequisites

- SB15 completed

## Exact Source References

- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/CanDoItAll.AgentFramework.WorkflowExecutors.Core.csproj`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowExecutorServiceCollectionExtensions.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `bundle://templates/02-subproject-template.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Add generic memory workflow executor using the shared operation handler and the same provider selection policy as tools.
- Add workflow/template configuration for query, ingestion, feedback submission, operation wait/poll policy, and source snapshot attachment.
- Provide compatibility mapping from old native memory executor ids only as a migration shim with documented lifetime.
- Support async provider operations by returning operation status or waiting within bounded policy, never by unbounded blocking.
- Add workflow tests for provider selection, async operation handling, source snapshot use, and old-template compatibility where required.
- Implement the generic executor through the current `IWorkflowExecutor` contracts, descriptor/source registration, and service collection patterns; do not introduce a memory-only workflow runtime.
- Return typed no-provider or capability-mismatch execution results when no provider is configured or allowed.

## Dependency Impact

- Workflow and process memory steps depend on generic executor behavior.

## Validation Depth

- `MAF workflow integration`

## Implementation Steps

1. Implement executor boundary in the generic workflow executor area and avoid native Cognitive Memory references.
2. Map workflow inputs to Memory Protocol request envelopes with process/workflow/step context.
3. Reuse the shared operation handler for dispatch and status instead of duplicating tool logic.
4. Add migration compatibility only where existing process templates would otherwise break, and mark removal conditions.
5. Add tests for executor sync result, async accepted result, provider mismatch, and old id compatibility.
6. Add registration tests proving the generic memory executor is discoverable through the current workflow executor infrastructure.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- Workflow memory execution and MAF tool memory execution are behaviorally equivalent for the same provider/request.
- Workflow steps can specify provider id or capability policy without hardcoding native Cognitive Memory.
- Any compatibility shim is isolated and scheduled for retirement after native extraction.

## Proof Required

- Create `proof/SB17/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run workflow executor tests for sync, async, provider selection, template configuration, and old-id compatibility.
- Run anti-duplication audit proving executor dispatch calls the shared handler.
- Run no-provider workflow execution tests proving no hidden fallback provider is selected.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB17 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Completion Proof

- Manifest: `bundle://proof/SB17/manifest.md`
- Semantic invariants: `bundle://proof/SB17/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB17/transcripts/failing-first-memory-workflow-executor-tests.txt`
- Focused workflow executor tests: `bundle://proof/SB17/transcripts/passing-memory-workflow-executor-tests.txt`
- Native dependency audit: `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-boundary.txt`
- Dispatch boundary audit: `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-dispatch-boundary.txt`
- Solution build: `bundle://proof/SB17/transcripts/passing-solution-build.txt`
- Template compatibility scope: no shipped workflow template currently references old native memory executor ids; compatibility is isolated in `MemoryWorkflowExecutorCompatibility`.
- Browser validation: `N/A`

## Suggested Agent Prompt

```text
Implement subbundle SB17 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
