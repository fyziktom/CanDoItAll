# 03 Process Workflow Memory Event Boundaries

## Status

- Ready after source snapshot shape is stable.

## Objective

- Add read-only source/event boundaries for process and workflow runtime evidence that future Cognitive Memory consolidation can consume.

## Covered Inputs

- PR-FR-004, PR-FR-005, PR-FR-006, PR-FR-007, PR-NFR-002, PR-NFR-003, PR-NFR-004, and PR-NFR-005.
- Source finding that process/workflow stores contain rich episodic and procedural evidence.

## Prerequisites

- `02-source-snapshot-read-models` must define reusable source item, cursor, hash, provenance, and permission concepts or explicitly decide separate contracts.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOutbox.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj

## Deliverables

- Process runtime evidence source contract.
- Workflow runtime evidence source contract.
- Adapter rules for decisions, artifacts, journals, conformance observations, improvements, events, executor metadata, and external requests.
- Tests for source item identity, hash stability, ordering, deletion/tombstone behavior, and permission context.

## Dependency Impact

- Cognitive Memory consolidation consumes process/workflow evidence through these boundaries.
- Existing process/workflow persistence remains authoritative.
- Runtime event streams and historical read models can evolve independently.

## Validation Depth

- Critical episodic/procedural source foundation.
- Integration tests are required because process/workflow records are cross-cutting.
- Regression tests must prove existing process/workflow behavior remains unchanged.

## Implementation Steps

- Define process and workflow source/evidence provider contracts.
- Map process decisions, artifacts, journals, observations, improvements, assignments, and run lifecycle data.
- Map workflow events, artifacts, external requests, executor ids, and run-store data.
- Add deterministic ordering and hash tests.

## Do Not Do

- Do not implement Cognitive Memory consolidation.
- Do not change process or workflow runtime behavior as part of this boundary.
- Do not expose sensitive payloads without permission/redaction metadata.
- Do not write back memory state into process or workflow tables.

## Acceptance Checklist

- Process and workflow evidence can be scanned or replayed deterministically.
- Sensitive payload handling is explicit.
- Future consolidation can create episodes/procedures without direct table coupling.

## Proof Required

- Unit tests for source evidence identity and hash semantics.
- Integration tests for representative process and workflow records.
- Dependency review showing no Cognitive Memory dependency from process/workflow modules.

## Browser Validation Logging

- No browser proof is required unless process/workflow UI changes unexpectedly.
- If UI changes happen, record route, viewport, and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to validation only after process/workflow evidence is source-grounded and behavior-compatible.

## Suggested Agent Prompt

- Implement process and workflow source/event boundaries for future memory consolidation, preserving existing runtime behavior and avoiding Cognitive Memory implementation.
