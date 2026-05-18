# 00 Prerequisite Boundary Gate

## Status

- Completed. Prerequisite boundaries implemented and validated.

## Objective

- Prove the required MAF context and source snapshot boundaries exist before any Cognitive Memory implementation begins.

## Covered Inputs

- User requirement to identify refactors before implementation.
- Live source audit of MAF context composition and Workbench project structure access.
- `analysis/03-prerequisite-refactor-decision.md`.

## Prerequisites

- The separate prerequisite bundle must be prepared and accepted.
- The follow-up `cognitive-memory-boundary-hardening` bundle must be closed before source ingestion, recall, or MAF integration implementation starts.
- No Cognitive Memory project references should be added before this gate closes.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Context\AgentContextContributionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Sources\MemorySourceSnapshotContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureSourceSnapshotProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeEvidenceSourceProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\WorkflowRuntimeEvidenceSourceProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\ProjectStructure\ProjectStructureRuntimeGatewayContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureRuntimeGateway.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\analysis\03-prerequisite-refactor-decision.md

## Deliverables

- Approved prerequisite-boundaries bundle.
- Approved boundary-hardening bundle with paging/cursor, redaction/hash, and MAF trace proof.
- Explicit go/no-go decision for Cognitive Memory implementation.
- Dependency impact note for MAF, Workbench, Process, and Workflow boundaries.

## Dependency Impact

- MAF must expose context contribution without private hardwiring.
- Source adapters must expose snapshots, hashes, cursors, and provenance.
- Cognitive Memory subbundles consume these boundaries instead of private implementation details.

## Validation Depth

- Source review is mandatory.
- Build/test proof belongs to the prerequisite-boundaries implementation bundle, not this architecture pass.

## Implementation Steps

- Confirm prerequisite bundle exists.
- Review the MAF context contributor boundary.
- Review source snapshot contracts for Workbench, Process, and Workflow sources.
- Record the gate result in `reviews/01-execution-report.md`.

## Do Not Do

- Do not implement Cognitive Memory before this gate is closed.
- Do not patch `MafAgentRuntime` with cognitive memory-specific logic as the first step.
- Do not read Workbench or Process EF entities ad hoc from Cognitive Memory.

## Acceptance Checklist

- Prerequisite bundle has source-grounded subbundles.
- MAF context contribution is an extension point, not a cognitive memory special case.
- Source snapshot contracts expose deterministic ids, hashes, cursors, provenance, and layout/reference data.
- Source snapshot contracts expose typed cursor failure, hash classification, and redaction-aware Workbench metadata.
- MAF context contribution traces are retained for future Cognitive Memory inspection.

## Proof Required

- Prepared-stage validation for the prerequisite bundle.
- Architecture review decision recorded in this bundle.
- File references and source evidence captured in review notes.
- Closure proof: `dotnet build .\CanDoItAll.slnx --no-restore` passed with 0 warnings and 0 errors.
- Closure proof: targeted context contributor, Workbench snapshot, and runtime evidence integration tests passed.
- Closure proof: `cognitive-memory-boundary-hardening` completed-stage validation passed after targeted unit and integration tests.
- Gate decision: Cognitive Memory implementation may proceed using these hardened boundaries; direct MAF private-provider edits and ad hoc source table reads remain out of bounds.

## Browser Validation Logging

- No browser proof is required for this architecture gate.
- UI proof starts in `08-human-review-ui`.

## Progression Gate

- Proceed to `01-module-foundation`; the prerequisite boundary and hardening decisions are accepted and validated.

## Suggested Agent Prompt

- Validate the prerequisite-boundaries bundle against the source evidence, then record whether Cognitive Memory implementation is allowed to start.
