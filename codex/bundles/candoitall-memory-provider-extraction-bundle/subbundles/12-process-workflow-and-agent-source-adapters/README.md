# 12 Process Workflow And Agent Source Adapters

## Status

- `Completed`

## Objective

- Add Source Gateway adapters for processes, workflow runs, artifacts, agent sessions, and process completion outcome snapshots.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R07
- R06

## Prerequisites

- SB10 gate passed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/CanDoItAll.AgentFramework.Workflows.Runtime.csproj`
- `repo://src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions/MemorySourceSnapshotModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/UnavailableProcessRuntimeEvidenceSourceProvider.cs`
- `bundle://architecture/04-runtime-operations-and-feedback.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement Source Gateway adapters for process definitions, process runs, workflow runs, workflow nodes, steps, artifacts, agent sessions, and process completion outcome snapshots.
- Map process/workflow/agent identifiers into structured memory execution context for queries, ingestion, and feedback.
- Capture process completion feedback hooks that can later submit delayed outcome feedback to the feedback ledger.
- Add artifact reference snapshots without copying large artifacts unless policy explicitly allows it.
- Add tests for step-specific context, agent-specific context, artifact references, process completion, and denied process scope.
- Migrate, adapt, or replace the current workflow runtime evidence source provider and unavailable-process source provider through the generic Source Gateway contract, preserving their failure diagnostics.

## Dependency Impact

- Feedback correlation and process completion feedback depend on these snapshots.

## Validation Depth

- `Source adapter foundation`

## Implementation Steps

1. Identify process, workflow, artifact, and agent-session read services that can be safely wrapped by adapters.
2. Implement snapshot shapes for process definition, running process, completed process, workflow node, step name, role, agent, and artifacts.
3. Connect process completion/outcome events to feedback candidate creation without making feedback mandatory for every process.
4. Add tests proving context requests include process step name and agent/requester identity when available.
5. Document how old native memory workflow executor ids should map to generic memory operation records.
6. Prove workflow/process source snapshots share the same contract family as SB04 and Workbench snapshots.

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
- Agent and workflow memory calls include process/workflow/step/session context when available.
- Process completion can later attach outcome feedback to delivered context packs.
- Provider source requests cannot query process data directly or bypass policy.

## Proof Required

- Create `proof/SB12/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run adapter tests for process, workflow, node, artifact, agent-session, and completion-outcome snapshots.
- Run negative tests for denied process scope and missing artifact access.
- Run compatibility tests for workflow runtime evidence and unavailable process source behavior.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start because SB12 proof is recorded, the acceptance checklist passed, and no phase-gate blocker remains.

## Closure Evidence

- Proof manifest: `bundle://proof/SB12/manifest.md`
- Semantic invariants: `bundle://proof/SB12/semantic-invariants.md`
- Focused runtime source adapter tests: `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt`
- Generic source gateway regression tests: `bundle://proof/SB12/transcripts/passing-source-gateway-tests.txt`
- Workbench source integration regression tests: `bundle://proof/SB12/transcripts/passing-workbench-source-integration-tests.txt`
- Full generic memory suite: `bundle://proof/SB12/transcripts/passing-memory-test-suite.txt`
- Solution build: `bundle://proof/SB12/transcripts/passing-solution-build.txt`
- Source audits: `bundle://proof/SB12/transcripts/source-audit-provider-driver-boundary.txt`, `bundle://proof/SB12/transcripts/source-audit-source-snapshot-contract-family.txt`, `bundle://proof/SB12/transcripts/source-audit-adapter-registration.txt`, and `bundle://proof/SB12/transcripts/source-audit-anti-stub.txt`
- Browser validation: `N/A`; this subbundle changed non-UI source adapters and runtime providers only.

## Suggested Agent Prompt

```text
Implement subbundle SB12 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
