# 18 Generic Agent Context Contributor And Hard Link Removal

## Status

- `Completed`

## Objective

- Replace direct Cognitive Memory context contributor with generic memory context contributor and remove native memory requirements from agent context construction.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R10
- R11

## Prerequisites

- SB16 and SB17 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs`
- `repo://src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions/MemorySourceSnapshotModels.cs`
- `bundle://inventories/02-dependency-and-removal-inventory.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Replace direct native `CognitiveMemoryAgentContextContributor` behavior with a generic memory context contributor that queries selected providers through the shared operation handler.
- Remove or isolate `CognitiveMemoryRecallWorkflowExecutor`, `CognitiveMemoryProbeWorkflowExecutor`, and learning proposal executor dependencies from generic MAF registration paths.
- Add provider selection and policy controls for automatic context contribution so memory use can be forced, disabled, or scoped per agent/workflow/process.
- Ensure context contribution handles async accepted operations without blocking agent startup or prompt construction.
- Add architecture guards preventing MAF projects from referencing native Cognitive Memory implementation projects or namespaces.
- Register the generic contributor through the current `IAgentContextContributor` / `MafAgentContextContributionProvider` path; do not create a memory-only context contribution runtime.
- Define the no-provider policy explicitly: skip contribution or emit a typed diagnostic according to configured policy, without calling native Cognitive Memory, Qdrant, OpenAI, or mock providers.

## Dependency Impact

- Base MAF closure depends on removing native `ICognitiveMemoryRecallOrchestrator` from context contribution.

## Validation Depth

- `Critical MAF decoupling`

## Implementation Steps

1. Inventory current `Advanced/CognitiveMemoryMafIntegration.cs` classes and classify each as generic replacement, native-only advanced surface, or temporary compatibility shim.
2. Implement generic context contributor using Memory Protocol request envelopes and the shared operation handler.
3. Move native-only probe/learning/professor flows behind provider-specific operations or native service APIs, not generic MAF dependencies.
4. Update DI registrations so MAF resolves generic memory contributor/tool/executor only.
5. Add tests and source audits for no native Cognitive Memory reference from MAF projects.
6. Add context contributor tests for no-provider skip/diagnostic behavior and current MAF registration.

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
- MAF no longer requires the native Cognitive Memory module to build or run generic memory tools/executors/contributors.
- Automatic context contribution can be scoped to selected providers and disabled by policy.
- Native advanced flows remain available only through provider-specific operations or UI surfaces.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB18/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB18/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB18/manifest.md` and `proof/SB18/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run architecture guard tests that fail on MAF references to `CanDoItAll.Modules.CognitiveMemory` or native `CanDoItAll.CognitiveMemory` implementation projects.
- Run context contributor tests for selected provider, disabled provider, async accepted result, and denied policy.
- Run registration tests proving the contributor participates through `MafAgentContextContributionProvider`.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB18 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Completion Proof

- Manifest: `bundle://proof/SB18/manifest.md`
- Semantic invariants: `bundle://proof/SB18/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB18/transcripts/failing-first-memory-context-contributor-tests.txt`
- Focused context contributor tests and architecture guard: `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt`
- Existing context contribution regression tests: `bundle://proof/SB18/transcripts/agent-context-contribution-regression-tests.txt`
- Native dependency audit: `bundle://proof/SB18/transcripts/source-audit-agentframework-context-no-native-memory-refs.txt`
- Native MAF registration removal audit: `bundle://proof/SB18/transcripts/source-audit-native-maf-registration-removal.txt`
- Dispatch boundary audit: `bundle://proof/SB18/transcripts/source-audit-memory-context-contributor-dispatch-boundary.txt`
- Anti-stub audit: `bundle://proof/SB18/transcripts/source-audit-memory-context-contributor-anti-stub.txt`
- Solution build: `bundle://proof/SB18/transcripts/passing-solution-build.txt`
- Browser validation: `N/A`

## Suggested Agent Prompt

```text
Implement subbundle SB18 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
