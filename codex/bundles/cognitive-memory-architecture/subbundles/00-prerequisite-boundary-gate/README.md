# 00 Prerequisite Boundary Gate

## Status

- Ready for architecture approval. No implementation started.

## Objective

- Prove the required MAF context and source snapshot boundaries exist before any Cognitive Memory implementation begins.

## Covered Inputs

- User requirement to identify refactors before implementation.
- Live source audit of MAF context composition and Workbench project structure access.
- `analysis/03-prerequisite-refactor-decision.md`.

## Prerequisites

- The separate prerequisite bundle must be prepared and accepted.
- No Cognitive Memory project references should be added before this gate closes.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\ProjectStructure\ProjectStructureRuntimeGatewayContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureRuntimeGateway.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\analysis\03-prerequisite-refactor-decision.md

## Deliverables

- Approved prerequisite-boundaries bundle.
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

## Proof Required

- Prepared-stage validation for the prerequisite bundle.
- Architecture review decision recorded in this bundle.
- File references and source evidence captured in review notes.

## Browser Validation Logging

- No browser proof is required for this architecture gate.
- UI proof starts in `08-human-review-ui`.

## Progression Gate

- Proceed to `01-module-foundation` only after the prerequisite boundary decision is accepted.

## Suggested Agent Prompt

- Validate the prerequisite-boundaries bundle against the source evidence, then record whether Cognitive Memory implementation is allowed to start.
