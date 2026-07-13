# Prioritized remediation roadmap

## Phase 0 — Safety rails before more retries

Goal: stop blind rework loops and make blockers diagnosable.

Tasks:

- Query execution observations by exact process run + step id.
- Show runtime receipt diagnostics when AgentFramework observation is missing.
- Add `BlockedStepPacket` to operator action and rework prompt.
- Include produced/required artifact descriptors, not just slot counts.
- Do not allow UI to recommend a blind retry when diagnostic is missing.

Exit criteria:

- The operator message for the same scenario tells whether the blocker is missing output artifact, missing child handoff, child no-go, missing tool, or unavailable observation.

## Phase 1 — Deterministic subprocess runtime bridge

Goal: make `StepKind=Subprocess` an orchestration primitive.

Tasks:

- Implement `ParentSubprocessArtifactBridge`.
- Runtime launches/waits/completes parent subprocess steps.
- Validate accepted child outputs and no-go child outputs from typed metadata.
- Synthesize parent managed artifact from child evidence.
- Keep agent-owned launch as backward-compatible fallback only.

Exit criteria:

- Parent `prepare-solution-skeleton` completes automatically when child `setup-handoff` or `setup-handoff-after-repair` exists.
- Parent blocks with concrete no-go if child `setup-repair-escalation` exists.

## Phase 2 — Artifact contract hardening

Goal: make artifacts inspectable, stable and grounded in managed file content.

Tasks:

- Add semantic artifact descriptors to runtime contract.
- Add actual managed artifact ref/content hash into produced artifact receipts.
- Fix artifact ledger to use applied result.
- Add typed materialization mode: `AgentWritten`, `RuntimeSynthesizedParentHandoff`, `RecoveredExistingProof`.

Exit criteria:

- Downstream steps can cite exact managed refs and content hashes.
- Missing output finalization cannot create misleading ledger events.

## Phase 3 — Capability/tool preflight

Goal: detect unavailable runtime tools before agent execution.

Tasks:

- Preflight exact required runtime tools from capability scope, launch context and subprocess contract.
- Compare required tools with composed providers for the actual governed process context.
- Produce deterministic missing-tool diagnostics and rebind/recovery suggestions.

Exit criteria:

- A missing `project_structure_process_subprocess_launch` or `workspace_dotnet_build` is caught before LLM execution.

## Phase 4 — Template schema hardening

Goal: move hard gates out of prose.

Tasks:

- Add `SubprocessContract` metadata.
- Add `CompletionGates` and `BranchRules` for existing templates.
- Validate templates at load time.
- Keep markdown short and explanatory.

Exit criteria:

- Template loader rejects a subprocess step with accepted repair path in prose but not in machine-readable metadata.

## Phase 5 — Regression harness

Goal: prevent recurrence in multi-team nested development process.

Tasks:

- Build in-memory integration tests for parent/child process runtime.
- Add adapter tests for missing/accepted/no-go child evidence.
- Add observation tests with many execution runs in the same process run.
- Add template validation tests.

Exit criteria:

- The current `prepare-solution-skeleton` scenario is covered without relying on a live LLM.
