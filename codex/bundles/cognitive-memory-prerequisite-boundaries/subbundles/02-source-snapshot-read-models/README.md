# 02 Source Snapshot Read Models

## Status

- Ready after contract location decision.

## Objective

- Add read-only Workbench/project-structure source snapshot contracts that expose deterministic source evidence for future Cognitive Memory ingestion.

## Covered Inputs

- PR-FR-003, PR-FR-006, PR-FR-007, PR-NFR-002, PR-NFR-003, PR-NFR-004, and PR-NFR-005.
- Source finding that `IProjectStructureRuntimeGateway` is agent-oriented, not a high-volume memory source contract.

## Prerequisites

- Contract location decision from `01-maf-context-contribution-boundary` if shared abstractions are introduced.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\ProjectStructure\ProjectStructureRuntimeGatewayContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureRuntimeGateway.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchSchemaInitializer.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj

## Deliverables

- Read-only source snapshot contract for project/workbench structure.
- Source item model with stable key, content hash, timestamps, provenance, layout metadata, links, references, and optional storage reference.
- Workbench adapter implementation.
- Tests for deterministic source keys, hash changes, cursor behavior, and metadata-backed Z/layout extension.

## Dependency Impact

- Cognitive Memory Workbench ingestion consumes this provider.
- Workbench remains authoritative for raw project data.
- Future schema changes can be hidden behind the provider contract.

## Validation Depth

- Critical source foundation.
- Unit and integration tests required.
- Source review must confirm no Cognitive Memory durable model dependency is added to Workbench.

## Implementation Steps

- Define snapshot request, cursor, manifest, item, link, layout, and provenance records.
- Implement Workbench snapshot adapter using existing Workbench data.
- Include metadata-backed Z/layout extension in item metadata without requiring immediate schema migration.
- Add deterministic hash and cursor tests.

## Do Not Do

- Do not implement Cognitive Memory source ingestion.
- Do not mutate Workbench records.
- Do not make generated summaries source truth.
- Do not introduce a Workbench dependency on Cognitive Memory entities.

## Acceptance Checklist

- Snapshot output has stable ids and hashes.
- Links and layout metadata are represented.
- Cursor resume behavior is defined and tested.
- Cognitive Memory bundle can reference the provider as its ingestion prerequisite.

## Proof Required

- Snapshot unit tests.
- Workbench integration tests.
- Dependency review showing Workbench does not reference Cognitive Memory.

## Browser Validation Logging

- No browser proof is required unless Workbench UI changes unexpectedly.
- If UI changes happen, record route, viewport, and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to process/workflow source boundaries only after snapshot identity, hash, and provenance semantics are stable.

## Suggested Agent Prompt

- Implement read-only Workbench/project-structure source snapshot contracts and tests, without implementing Cognitive Memory ingestion or changing Workbench UI behavior.
