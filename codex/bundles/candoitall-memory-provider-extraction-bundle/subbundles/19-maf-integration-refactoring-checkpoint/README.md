# 19 Maf Integration Refactoring Checkpoint

## Status

- `Completed`

## Objective

- Refactor and harden MAF memory integration, dependency guards, provider selection, templates, and compatibility behavior before UI work.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R09
- R10
- R11
- R20

## Prerequisites

- SB15-SB18 completed

## Exact Source References

- `bundle://plan/02-checkpoints.md`
- `bundle://inventories/02-dependency-and-removal-inventory.md`
- `bundle://architecture/07-testing-and-mocking-strategy.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Audit SB15-SB18 for duplicate tool/executor logic, hidden native references, missing provider selection, weak async handling, and overgrown MAF integration files.
- Extract shared MAF memory request builders, result shapers, policy resolvers, and diagnostics helpers into bounded files.
- Strengthen architecture guard tests for MAF dependency direction and old native executor id retirement plan.
- Verify generic context contributor, tool provider, and workflow executor all use the same operation handler and feedback correlation path.
- Block UI work until MAF memory integration is generic and stable.
- Verify MAF integration uses current `IAgentRuntimeToolProvider`, `IWorkflowExecutor`, and `IAgentContextContributor` registration paths.
- Verify no-provider behavior is consistent across tool, workflow executor, and context contributor paths.

## Dependency Impact

- Blocks UI and native extraction if MAF still knows native memory or has duplicate operation paths.

## Validation Depth

- `Critical checkpoint`

## Implementation Steps

1. Run dependency audit across all MAF projects for native memory namespaces and project references.
2. Search for multiple memory operation dispatch paths and refactor duplicates back to the shared handler.
3. Inspect provider-selection code for global defaults that ignore agent/workflow/process configuration.
4. Review async operation handling to ensure no unbounded wait is introduced in agent prompt construction or workflow execution.
5. Record checkpoint result and reopen SB15-SB18 if any MAF boundary invariant fails.
6. Run no-provider scenarios across all three MAF entry points and fail the checkpoint if any path falls back silently.

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
- MAF memory integration can be tested with mock providers and without native Cognitive Memory projects loaded.
- Provider selection works through the same policy path in tools, workflow executors, and context contributors.
- MAF entry points use the current MAF extension points, not parallel memory-only registration or dispatch infrastructure.
- No downstream UI/native extraction phase needs to compensate for MAF-native coupling.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB19/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB19/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB19/manifest.md` and `proof/SB19/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Capture dependency audit output and duplicate-handler audit output.
- Run MAF memory tool/executor/context contributor tests after refactoring.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Completion Proof

- Proof manifest: `bundle://proof/SB19/manifest.md`.
- Semantic invariants: `bundle://proof/SB19/semantic-invariants.md`.
- Failing-first checkpoint test transcript: `bundle://proof/SB19/transcripts/failing-first-maf-integration-checkpoint-tests.txt`.
- Focused passing checkpoint transcript: `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt`.
- MAF memory regression transcript: `bundle://proof/SB19/transcripts/passing-maf-memory-regression-tests.txt`.
- Native dependency audit: `bundle://proof/SB19/transcripts/source-audit-maf-native-dependency-boundary.txt`.
- Dispatch boundary audit: `bundle://proof/SB19/transcripts/source-audit-maf-memory-dispatch-boundary.txt`.
- Duplicate logic audit: `bundle://proof/SB19/transcripts/source-audit-maf-memory-duplicate-logic.txt`.
- Anti-stub audit: `bundle://proof/SB19/transcripts/source-audit-maf-memory-anti-stub.txt`.
- File-size audit: `bundle://proof/SB19/transcripts/source-audit-maf-memory-file-size.txt`.
- Solution build: `bundle://proof/SB19/transcripts/passing-solution-build.txt`.
- Browser validation: `N/A`; SB19 has no browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB19 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB19 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
