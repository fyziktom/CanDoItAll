# 35 Architecture Re-entry And Characterization Gate

## Status

- `Completed`

## Objective

- Reopen the previously completed extraction bundle against the live main and Cognitive Memory repositories, record the corrected C# architecture, and capture failing-first characterization proof before any repair production code is edited.

## Success Criteria

- The current dependency graph, capability-grouping partial classes, agent-memory runtime paths, provider-selection behavior, operation ownership gap, transport configuration, and external-service security boundary are inventoried from live code.
- The architecture records define one owner for agent-memory settings, runtime routing, generic memory operations, transport adapters, and the external Cognitive Memory implementation.
- Focused characterization tests reproduce the known defects without changing production behavior.
- The prepared-stage bundle validator passes, and the architecture gate explicitly authorizes SB36 production edits.

## Covered Inputs

- R19
- R20
- R21
- R22
- R23
- R24
- R25
- R26
- R27
- R28
- R29

## Prerequisites

- SB34 historical completion is preserved as evidence, but its closure claim is superseded by this re-entry gate.
- Both `CanDoItAll` and `CanDoItAll.CognitiveMemory` working trees are available and their starting revisions are recorded.

## Exact Source References

- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://requirements/03-non-negotiable-boundaries.md`
- `bundle://architecture/00-csharp-current-state-inventory.md`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `bundle://plan/architecture-checkpoints.md`
- `bundle://reviews/csharp-architecture-gate.md`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryProviderRegistry.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Memory/AgentMemoryAccessMetadata.cs`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Context/MemoryAgentContextContributor.cs`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Tools/MemoryAgentRuntimeToolProvider.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/HostCompositionDependencyRemovalTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryProviderRegistryTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryOperationHandlerTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MemoryAgentContextContributorTests.cs`

## Deliverables

- Replace the stale live-reentry analysis with a dated inventory of both repositories, current commits, current builds/tests, project references, partial-class clusters, and unsupported or overclaimed protocol capabilities.
- Complete the seven C# architecture artifacts listed in Exact Source References with named project and namespace owners, allowed dependencies, forbidden dependencies, selected patterns, direct test seams, and partial-class decisions.
- Add a sequential repair traceability map from R22-R29 to SB36-SB40 and to observable acceptance signals.
- Add focused characterization tests that fail for implicit provider fallback, cross-requester operation status/cancel access, missing `/mem:` behavior, missing project context propagation, configuration-extension loss, and unauthenticated or cross-project native recall.
- Record the existing main memory-suite failures caused by direct host composition references to `CanDoItAll.Modules.CognitiveMemory` as a baseline defect, not as an accepted test exception.
- Run prepared-stage bundle validation and capture an explicit architecture readiness decision.

## Dependency Impact

- SB36-SB40 are invalid if this gate does not identify the live owners, dependency directions, behavioral failures, and proof seams first.
- Characterization test names and expected failing assertions become the failing-first evidence that later subbundles must turn green without weakening.

## Validation Depth

- `Critical architecture and failing-first readiness gate`

## C# Architecture Impact

- This phase changes bundle architecture artifacts and characterization tests only. It must not change production types, DI registration, persistence, protocol behavior, UI behavior, or external-service behavior.
- The architecture inventory must classify every non-generated partial class in generic memory and MAF memory integration as keep, merge, or extract, with capability-grouping partials classified as prohibited.
- The proposed project map must keep generic memory independent of native Cognitive Memory and move MAF-specific orchestration out of the generic Memory Application layer.

## Boundary Ownership

- `CanDoItAll.Memory.Abstractions` owns provider-neutral protocol and identifier contracts.
- `CanDoItAll.Memory.Application` owns one-provider operation orchestration and generic policy enforcement.
- A dedicated Agent Framework memory integration project owns agent settings interpretation, directive parsing, multi-provider routing, tool exposure, context contribution, and workflow adaptation.
- HTTP and MCP projects own transport mapping only; module projects own Blazor UI and host composition only.
- `CanDoItAll.CognitiveMemory` owns native domain, persistence, access policy, workers, and service hosting in its own repository.

## Dependency Direction

- Allowed direction: module UI/composition -> Agent Framework memory integration -> Memory Application -> Memory Abstractions.
- Allowed direction: HTTP/MCP/Persistence adapters -> Memory Application and Memory Abstractions.
- Allowed direction: Cognitive Memory service -> versioned provider-neutral protocol contract, never sibling main-host implementation projects.
- Forbidden direction: Memory Application -> Agent Framework Core, MAF/module -> native Cognitive Memory implementation, or base composition -> `CanDoItAll.Modules.CognitiveMemory`.

## Pattern Decision

- Record a thin facade plus cohesive command/query handlers for generic operation dispatch.
- Record a strategy/catalog decision for provider selection and an explicit authorizer for operation ownership.
- Record a parser plus routing policy for prompt directives and multi-provider fan-out.
- Record request/response mapper boundaries for transports; reject service-locator, capability-bucket helper, and partial-class grouping alternatives.

## Testability Contract

- Each new policy or parser must be constructible with explicit dependencies and testable without starting the web host.
- Failing-first tests must call current production selection, operation, MAF, serialization, and service endpoint paths; DTO-only assertions and fabricated success flags do not qualify.
- Cross-repository service characterization must exercise an HTTP host or `WebApplicationFactory`, not directly invoke a mapper as a substitute for authorization.

## Partial Class Policy

- Generated regex code, Razor component code-behind, platform-specific generated code, and EF migrations may remain partial.
- `MemoryOperationHandler`, HTTP/MCP drivers, event workers, retention stores, settings codecs, and runtime routing may not use partial classes to group capabilities.
- SB35 records the baseline audit only; production removals occur in SB36-SB39.

## Architecture Proof Required

- Capture a scoped CodeAnalytics or equivalent dependency snapshot for both repositories and list project/type cycles relevant to memory.
- Capture an `rg` audit of partial declarations, project references, native namespaces, direct Qdrant references, and sibling-repository references.
- Record architect review outcomes in `bundle://reviews/csharp-architecture-gate.md`, including any blocked decision with owner and consequence.
- Prove the proposed map has no dependency path from generic Memory Abstractions/Application to native Cognitive Memory or module UI.

## Implementation Steps

1. Record clean/dirty state, commits, SDK version, solution inventory, and baseline build/test results for both repositories.
2. Inventory project references and type responsibilities for generic operations, MAF integration, transports, persistence, UI, and the external service.
3. Locate all partial declarations and classify each against the partial-class policy.
4. Write the boundary map, dependency rules, pattern records, and testability plan.
5. Add narrow failing characterization tests for each defect assigned to SB36-SB39 without editing production code.
6. Update requirement traceability and architecture checkpoints.
7. Run prepared-stage bundle validation and the architecture review gate; stop if either fails.

## Scope Exceptions

- No production fix is allowed in this subbundle, even when a characterization test exposes a trivial repair.
- Native absolute paths are local execution aids because the external repository is a separate Git root; durable architecture and proof references must also use `bundle://` paths.

## Do Not Do

- Do not edit any file under `src` in either repository.
- Do not make a failing characterization test pass by weakening its assertion or seeding a test-only success path.
- Do not mark historical SB34 proof as current proof.
- Do not accept the existing host-composition test failures as expected debt.

## Acceptance Checklist

- All seven architecture artifacts exist and agree on ownership and dependency direction.
- Every R22-R29 requirement is assigned to exactly one primary repair subbundle and at least one final SB40 regression.
- Characterization tests fail for the intended semantic reason and their transcripts identify production call sites.
- Baseline passing suites are recorded so later phases can distinguish regression from pre-existing failure.
- The partial-class inventory covers main generic memory, Agent Framework memory integration, and relevant external provider code.
- Prepared-stage validator and architecture gate pass before SB36 begins.

## Proof Required

- Create `proof/SB35/manifest.md` and `proof/SB35/semantic-invariants.md` with changed-file hashes, repository revisions, command transcripts, and portable references.
- Failing-first proof: run each new characterization test and capture the expected semantic failure separately from compile or setup failures.
- Positive proof: run the existing focused memory and MAF suites that currently pass and record exact counts.
- Negative proof: show production source remains unchanged and the architecture gate rejects a dependency reversal or capability-grouping partial fixture.
- Anti-stub proof: demonstrate that each failing test reaches the current production registry, handler, MAF contributor/driver, or hosted endpoint and cannot pass through a hand-built response.
- Run the bundle validator in prepared stage and record its zero-error transcript.

## Browser Validation Logging

- N/A. This subbundle permits no browser-visible production change. Record N/A in the execution report.

## Progression Gate

- No production edit for SB36-SB40 may start until SB35 characterization proof is captured, prepared-stage validation passes, and `bundle://reviews/csharp-architecture-gate.md` records `PASS` for implementation readiness.

## Suggested Agent Prompt

```text
Implement SB35 only. Reopen the bundle against both live repositories, write and validate the C# architecture artifacts, add failing-first characterization tests, and do not edit production code. Stop unless the prepared bundle and architecture readiness gates pass honestly.
```
