# SB13-process-semantics-and-recovery-extraction: Extract process semantics, provider policy, and artifact recovery from MAF/generic runtime

## Metadata

- Phase: D — Dependency direction and process ownership
- Depends on: `SB12-maf-dependency-graph-repair`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Move process-step status/path/recovery/provider-selection semantics to Processes while giving generic execution a provider-neutral policy and recovery pipeline.

## Why this subbundle exists

`ProcessArtifactRecoveryService` in MAF knows exact process artifact paths and outcomes. Generic execution also branches on source strings for criticality/provider override. These rules belong to the process integration and must still preserve current hardening and completion gates.

## Scope

- Introduce generic policy/recovery contracts.
- Make MAF return typed failure/partial evidence.
- Implement process recovery/provider/criticality policies in Modules.Processes.
- Route recovered output through normal process completion.
- Remove process symbols and source-string branches from MAF; reduce them in generic Core to typed policy snapshots.

## Non-goals

- Do not weaken process completion, artifact, receipt, finalizer, mutation, or grounding gates.
- Do not let MAF call Processes through reflection/service location.

## Required SharedInfo skills

- `csharp-provider-tool-plugin-isolation`
- `csharp-project-boundary-extraction`
- `csharp-testability-contracts`
- `canonical-model-review`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Define runtime-neutral evidence/failure types for cases currently triggering process artifact fallback, including provider stream timeout and missing required finalizer. Include bounded tool traces, output contract identity, execution IDs, provider/model and safe diagnostics.
2. Define `IAgentExecutionOutcomeRecoveryPolicy` and coordinator. Policies return a typed decision such as `NotApplicable`, `Recovered`, or `Rejected`, with machine output/evidence and diagnostics. Core validates and persists; policy does not mutate the run store directly.
3. Change MAF generic finalizer/provider paths to return/throw the typed failure/partial evidence after MAF-native bounded repair is exhausted. Delete process artifact reading/synthesis from MAF.
4. Move `ProcessArtifactRecoveryService` behavior into a Processes-owned `ProcessAgentExecutionOutcomeRecoveryPolicy`. Preserve all current hardening:
      - exact current run/step identity;
      - exact primary managed artifact path;
      - successful current-execution overwrite trace;
      - canonical Status parsing;
      - concrete blocker evidence;
      - no historical artifact promotion.
5. Ensure recovered `Completed` and other outcomes enter the same process result conversion, managed artifact materialization, `ProcessStepCompletionCoordinator`, receipt/grounding/mutation/branch gates, acceptance append, and hashing path as ordinary completion.
6. Extract process-specific provider override into `IExecutionProviderSelectionPolicy`, implemented by Processes. Generic Core invokes policies based on typed admission policy, not literal `sourceKind == process-step` plus output CLR type.
7. Replace generic `IsGovernedMachineCriticalRun` source-string decisions with an immutable admission criticality/output policy record where practical. The Processes adapter sets it. Keep compatibility mapping only while this subbundle is in progress; all source-string process branches must be removed before SB14 can unlock.
8. Move process-specific tool-denial guidance/policy contribution out of generic MAF. Generic tool policy can enforce generic operation/resource rules; Processes contributes process operation/managed-artifact policy through typed metadata/strategy.
9. Delete `Runtime/Execution/ProcessArtifactRecoveryService.cs` from MAF and add architecture scans for process types, status names, managed path templates, and `process-step` literals.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Core/application owns generic recovery orchestration and policy contracts. MAF reports adapter-level failure evidence. Modules.Processes owns process provider selection, criticality/output policy, managed artifact recovery and outcome translation. Processes Runtime remains canonical for process state.

## Dependency Direction

Processes -> AgentFramework contracts/Core. MAF -> Runtime contracts/Core narrow evidence types. No AgentFramework/MAF -> Processes or Modules.Processes reference.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Policy/strategy contribution with typed recovery opportunity. Avoid a generic callback that exposes MAF SDK objects or lets arbitrary policies mutate execution state directly.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Direct unit tests for process recovery policy success and every rejection condition.
- MAF adapter test proves missing finalizer returns generic typed evidence, not a process outcome.
- Process provider selection policy tests preserve current candidate ordering/feature requirements.
- Integration: typed MAF failure -> recovery coordinator -> Processes policy -> ordinary completion gates.
- Negative: stale/wrong artifact, no current write trace, status-only Blocked, wrong scope/contract all fail closed.
- Architecture scan finds no process semantics in MAF.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No `ProcessStepOutcomeResult`, `ProcessStepOutcomeStatus`, `ProcessArtifactRecovery`, process artifact path, or `"process-step"` in MAF production source.
- No MAF reference to Processes.
- Recovered process output enters ordinary completion coordinator.

Other required proof:

- changed-file and changed-project list;
- before/after responsibility ownership;
- CodeAnalytics snapshot/dependency evidence when available;
- build and test transcripts;
- direct testability proof;
- old-owner shrink/deletion proof;
- no-new-caller proof for compatibility facades;
- privacy/logging review when context or tool data changes.

## Validation commands

- `MAF and Processes builds.`
- `Focused Unit/Integration process recovery/completion suites.`
- `Architecture guard and dependency audit.`
- `Existing Calculator/Tetris/process hardening regressions when available.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Process semantics have one owner in Processes.
- MAF returns generic evidence only.
- No completion gate is bypassed.
- Provider/criticality decisions are typed policies rather than MAF/process string leaks.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- The proposed recovery policy must receive MAF SDK objects. Map to runtime-neutral evidence first.
- Recovered output bypasses normal completion gates.
- Any process behavior is copied and left active in MAF.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- generic recovery/provider policy contracts
- Processes implementations
- deleted MAF process recovery
- integration tests
- source/dependency proof

## Downstream unlock

SB14 checkpoint may start after process-leak scans and recovery integration pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Process artifact recovery currently depends on exact current-run write traces and canonical status parsing. Moving it must preserve every fail-closed condition.
- Recovery must re-enter `ProcessStepCompletionCoordinator` and ordinary completion gates; direct result conversion is unsafe.
- Governed-process provider selection, structured-output requirements, finalizer validation, and process-specific operation policy are separate concerns and may require more than one Processes-owned strategy.
- Rollback must disable recovery and fail closed; it must never restore process code to MAF.

## Safe cutover sequence

1. Add Processes-owned policies and characterize current recovery decisions.
2. Route MAF failures/evidence to the new policies while legacy code remains disabled by a single selector.
3. Compare only pure recovery decisions; never materialize artifacts or submit completion twice.
4. Switch to the Processes path and prove it runs the ordinary completion coordinator/gates.
5. Delete MAF/generic process branches only after SB14 acceptance.

## Post-change verification and bugfix procedure

1. Reproduce with fixed operation/run/session/context/authority/scope identifiers and a fake provider or deterministic fixture where possible.
2. Identify the failing stage from persisted activity and telemetry before editing: admission, context, authority, scope, composition, provider, session, tool, approval, output/finalizer, persistence, process, workflow, or UI refresh.
3. Add a failing regression test at the owner boundary. Do not patch the caller merely because the symptom appears there.
4. Compare against SB00 characterization/golden evidence and inspect changed project references and runtime/tool manifests.
5. Apply the smallest cohesive fix, then run focused tests, architecture guards, and the current checkpoint suite.
6. Update `proof/proof-manifest.json`, the risk register, and `proof/SESSION-HANDOFF.md` with the root cause and remaining uncertainty.

## Durable session handoff

Before ending a Claude Code session, update `proof/SESSION-HANDOFF.md` with:

- current commit and working-tree state;
- completed checklist items and changed files;
- exact commands and test results;
- CodeAnalytics snapshot/dependency evidence;
- selected cutover path/flag and observed telemetry;
- unresolved failures with correlation IDs and owning stage;
- the next smallest safe action;
- anything a fallback Claude model must not redo or reinterpret.

Do not rely on chat history as the only handoff mechanism.
