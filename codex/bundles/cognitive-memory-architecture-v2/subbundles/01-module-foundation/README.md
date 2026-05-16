# 01 Module Foundation

## Status

- Ready after prerequisite gate.

## Objective

- Establish the Cognitive Memory module boundary, durable state model, registration path, storage references, policy abstractions, and test seams.

## Covered Inputs

- Requirements FR-002, FR-004, FR-021, NFR-002, NFR-008, NFR-009, NFR-012, and NFR-013.
- Target solution and module-boundary architecture.

## Prerequisites

- `00-prerequisite-boundary-gate` must be closed.
- Module shape must preserve existing CanDoItAll registration and EF configuration conventions.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContextModelRegistry.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\DependencyInjection\InfrastructureServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\01-target-solution.md

## Deliverables

- Module/project registration design.
- EF entity/configuration design for source manifests, memory records, relations, projection state, recall traces, review items, and run records.
- Strongly typed mode, policy, version, and identity contracts.

## Dependency Impact

- Composition adds a module marker and service registration.
- Infrastructure consumes EF configurations through the existing registry.
- Tests use fake sources, fake embedding providers, fake RAG drivers, and fake policy contexts.

## Validation Depth

- Unit tests for identity, hash, mode, version, and policy objects.
- Integration test proving EF model discovery works without direct `DbSet<T>` additions.

## Implementation Steps

- Define foundation contracts and entity boundaries.
- Add module registration using existing patterns.
- Add persistence configurations and migrations.
- Add deterministic identity and hash tests.

## Do Not Do

- Do not add direct dependencies from existing modules into Cognitive Memory.
- Do not put Qdrant-specific fields in canonical memory entities.
- Do not use stringly typed modes or source kinds.

## Acceptance Checklist

- Durable memory state is independent from projections.
- Every persisted generated record has source evidence or explicit generated reason.
- Modes and algorithm versions are persisted where they affect behavior.

## Proof Required

- `dotnet build` for touched projects.
- Targeted unit/integration tests for EF registration and core identities.

## Browser Validation Logging

- No browser proof is required for foundation-only work.
- Any accidental UI change fails this subbundle scope.

## Progression Gate

- Proceed to source ingestion only when durable identity, provenance, and mode/version contracts are stable.

## Suggested Agent Prompt

- Implement only the Cognitive Memory foundation contracts, module registration, persistence model, and tests described by this subbundle.
