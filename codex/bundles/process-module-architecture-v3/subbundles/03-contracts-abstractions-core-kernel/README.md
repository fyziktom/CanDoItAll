# SB03 Contracts, Abstractions, Core Kernel, And Invariants

## Status

- Completed

Executed on 2026-06-15. Closure proof is recorded under `proof/SB03`.

## Objective

Create stable external contracts, generic abstractions, and pure core/kernel rules for process definitions, instance plans, branch contracts, artifacts, runtime events, loop fingerprints, and state transition definitions.

## Why This Bundle Exists

Everything later depends on generic contracts staying clean. This bundle prevents domain terms, EF shapes, UI concerns, and dispatcher behavior from entering the kernel.

## Covered Inputs

- REQ-001 through REQ-005.
- REQ-042 through REQ-045.
- v3 core, branch, and dependency architecture.

## Context Reset: Read These First

- SB02 execution report.
- `architecture/03-core-model-and-invariants.md`
- `architecture/11-project-boundary-and-dependency-map.md`
- `architecture/13-branch-switch-and-loop-contract.md`
- `validation/02-architecture-test-plan.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/03-core-model-and-invariants.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/11-project-boundary-and-dependency-map.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/13-branch-switch-and-loop-contract.md`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs`

## Source Evidence To Use

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs`
- SB01 reference archive for old core/routing/subprocess rules.

## Prerequisites

- SB02 complete.
- Skeleton projects compile.
- Boundary tests exist.

## In Scope

- Strongly typed IDs.
- External DTO schema/version markers.
- Generic capability tags.
- Artifact definition/slot/reference core model.
- Branch definition/outcome/route/loop core model.
- Runtime event envelope contracts.
- State transition definitions.
- Loop fingerprint rules.
- Pure validation rules.

## Out Of Scope

- No EF persistence.
- No dispatcher implementation.
- No concrete drivers.
- No UI projections.
- No template Git workflow.

## Target Projects / Files

- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Abstractions`
- `src/CanDoItAll.Processes.Core`
- tests for contracts/core/boundaries.

## Deliverables

- Stable contracts and core rule library.
- Pure unit tests.
- Architecture dependency tests.
- Domain vocabulary leak tests.

## Expected Deliverables

- Branch routing model cannot be implemented through free-text token matching.
- Runtime event envelope requires schema version, correlation, causation, actor, sensitivity, and UTC timestamp.

## Dependency Impact

- SB04-SB14 depend on these contracts.
- Any breaking change after this bundle requires explicit migration notes.

## Validation Depth

- Validate with pure core unit tests, contract serialization tests, forbidden dependency tests, domain vocabulary leak tests, and negative branch/artifact/state fixtures.

## Architecture Invariants That Must Hold

- Core has no EF, Razor, infrastructure, concrete driver, Git implementation, AgentFramework runtime, or storage reference.
- Core uses opaque capability tags only.
- Display text cannot determine branch routing.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Define contract and abstraction primitives.
2. Define core graph/artifact/branch/state/event models.
3. Implement pure validators and fingerprint rules.
4. Add tests for valid and invalid definitions.
5. Add negative tests for domain leakage and forbidden references.

## Refactoring Review Checkpoint

- Extract pure rules into small deterministic classes.
- Keep DTOs separate from validators.
- Verify no large core service appears.

## Required Tests / Proof

- Unit tests for IDs, event envelopes, graph rules, artifact rules, branch rules, loop fingerprints, and state transitions.
- Architecture dependency tests.
- Domain vocabulary leak tests.

## Search Proof

- Search generic projects for banned domain terms.
- Search for EF/Razor/concrete driver references in core projects.

## Stop And Report Conditions

- Stop if a generic concept requires a domain-specific name.
- Stop if a later layer type is needed in core.
- Stop if branch semantics can only be expressed through display text.

## Do Not Do

- Do not add EF entities.
- Do not add UI models.
- Do not add concrete driver code.
- Do not route branches by free-text tokens.

## Acceptance Checklist

- [x] Contracts compile.
- [x] Core compiles.
- [x] Pure tests pass.
- [x] Dependency tests pass.
- [x] Domain leak tests pass.

## Proof Required

- Test output.
- Dependency scan output.
- Domain leak scan output.

## Browser Validation Logging

- Browser validation is not required because no UI behavior is implemented.

## Progression Gate

- SB04 and SB05 may start after core contracts and boundary tests pass.

## Suggested Agent Prompt

Execute SB03 from `codex/bundles/process-module-architecture-v3/subbundles/03-contracts-abstractions-core-kernel`. Build generic contracts/core only. Keep the kernel domain-neutral and persistence-free.

## Handoff Notes For Next Bundle

Record public contract names, test names, known extension points, and any deferred core questions.
