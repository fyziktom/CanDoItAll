# SB17-cross-cutting-cutover-stabilization-and-bugfixing: Cross-cutting cutover stabilization, fault injection, and owner-boundary bugfixing

## Metadata

- Phase: F — Cross-cutting stabilization and release
- Depends on: `SB15-versioned-runtime-state-and-continuation`, `SB16-lightweight-llm-invocation-foundation`
- Checkpoint: Yes
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Exercise the integrated context, scope, runtime-port, MAF, process, continuation, and lightweight-LLM architecture as one system; identify regressions at their true owner boundary; repair them with failing-first tests; and prove that production uses one side-effecting path per responsibility.

## Why this subbundle exists

Large architecture migrations often pass isolated tests yet fail when context capture, provider execution, approvals, persistence, process gates, and UI refresh interact. This subbundle provides a deliberate stabilization window before compatibility code is deleted.

## Scope

- Run the complete cross-boundary scenario and fault-injection matrix.
- Verify feature selectors and compatibility facades choose exactly one production path.
- Add bounded observability and a failure-stage taxonomy.
- Repair regressions in the owning layer with failing-first tests.
- Prove restart, profile-switch, navigation, and continuation behavior.
- Produce a cutover readiness and rollback report for SB18.

## Non-goals

- Do not redesign accepted architecture to hide a regression.
- Do not add new product features or ordinary-chat UI.
- Do not remove compatibility readers required by persisted state; SB18 owns deletion after readiness proof.

## Required SharedInfo skills

- `csharp-architecture-governor`
- `csharp-testability-contracts`
- `canonical-model-review`
- `csharp-architecture-review-gate`
- `csharp-dependency-graph-audit`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify all dependencies are `Unlocked`.
2. Record current HEAD and working-tree status.
3. Read `../../architecture/11-change-impact-and-adaptation-map.md` through `14-lightweight-llm-and-ordinary-chat-foundation.md`.
4. Read `../../plan/cutover-and-rollback-matrix.md` and `../../plan/observability-and-regression-plan.md`.
5. Refresh the CodeAnalytics snapshot and dependency graph.
6. Copy the proof manifest template and create `proof/SESSION-HANDOFF.md`.

## Detailed implementation tasks

1. Establish one correlation record containing operation ID, execution run ID, chat session ID, context capture ID/digest, authority ID/fingerprint, workspace scope identity, adapter/schema version, provider/model, and selected execution port. Never include raw prompts, attachments, secrets, or tool arguments.
2. Exercise the scenario matrix: Canvas -> Gantt, Project X -> Y, multiple floating chats, rapid navigation, profile switch, send during loading, context expiry, ordinary send, tool use, per-proposal approval, restart/resume, provider/model mismatch, process recovery, workflow LLM, and direct lightweight LLM.
3. Inject failures at every boundary and verify classification, persistence, cleanup, and rollback behavior.
4. Compare capability/tool manifests, approval requirements, usage, finalizer/tool trace ordering, runtime state, public API projections, and process receipts to SB00 evidence.
5. Scan production code for dual execution, dual writes, broad-runtime bypass, current-UI recapture, mixed scope services, service location, process leakage, first-wins tool collisions, and accidental agent construction in lightweight LLM paths.
6. For each defect, write a bugfix record with symptom, stage, evidence, owner, root cause, failing test, fix, focused validation, checkpoint validation, and architectural regression guard.
7. Produce a readiness decision: `Ready for cleanup`, `Blocked`, or `Ready with named compatibility readers retained`.

## C# Architecture Impact

This is an architecture-relevant stabilization subbundle. Every fix must preserve the accepted ownership and dependency direction. A green test achieved by widening authority or restoring an old coupling is a failure.

## Boundary Ownership

The diagnosed owner fixes the defect. UI fixes observations and lifecycle; application/core fixes coordination and persistence; scope factory fixes workspace services; provider runtime fixes dispatch; MAF adapter fixes SDK mapping; Processes fixes process semantics; workflow/lightweight LLM fixes direct invocation behavior.

## Dependency Direction

No new project reference is expected. Any proposed reference change reopens the relevant architecture checkpoint and requires a dependency audit.

## Pattern Decision

Use a strangler cutover with one selected production path and pure shadow comparison only. Use fault injection and differential fixtures for verification. Do not use dual side-effecting execution.

## Testability Contract

- Every bug has a failing regression test before the fix.
- Every boundary has at least one negative/fault test.
- Restart/legacy fixtures are file-based and deterministic.
- Cross-project authority and mixed-scope failures are explicit.
- Lightweight LLM tests prove absence of agent/session/tool construction.
- Process recovery tests prove ordinary completion gates run exactly once.

## Partial Class Policy

- Do not add partial runtime/service files as a stabilization shortcut.
- Do not restore deleted behavior in the old facade.
- Do not introduce generic helpers that mix failure stages.

## Architecture Proof Required

- Full affected-caller scan.
- Dependency/cycle report.
- Single-path cutover proof.
- Fault-injection matrix and transcripts.
- Bugfix records and regression tests.
- Public-projection and sensitive-data review.
- Readiness/rollback decision.

## Validation commands

- Run `../../scripts/run-validation.ps1` against the target repository.
- Run targeted context, runtime, provider, process, workflow, API, and component filters during diagnosis.
- Run `check_cutover_guards.py` and `scan_affected_runtime_callers.py`.
- Perform manual rebuilt-app Canvas/Gantt/floating-chat acceptance.

## Acceptance criteria

- One side-effecting production path owns every responsibility.
- All high-risk scenarios and faults have deterministic outcomes.
- Regressions are fixed at owner boundaries with tests.
- No authority, scope, process, SDK, or state-envelope leakage reappears.
- A documented rollback exists for every remaining selector.

## Stop and repair conditions

Stop cleanup readiness when a defect is fixed by broadening UI authority, recapturing current context for continuation, mixing scope bundles, bypassing completion gates, restoring product references in MAF, or delegating lightweight LLM calls to the full agent runtime.

## Required deliverables

- completed CP5 checkpoint result and cutover readiness report
- fault-injection results
- bugfix records and tests
- updated risk/rollback matrix
- completed proof and session handoff

## Downstream unlock

SB18 may start only after the CP5 checkpoint records `Ready for cleanup` or `Ready with named compatibility readers retained`, an `Unlocked` downstream decision, and no authority/dependency/scope/state/side-effect blocker.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Feature flags and compatibility facades can accidentally leave two production paths. Dual provider/tool execution or dual mutations are prohibited.
- Bugs often surface across boundaries: a context mismatch may look like a tool denial, a runtime-state mismatch like a provider failure, and a persistence race like an approval bug.
- Stabilization must exercise failures and restarts, not only happy-path smoke tests.
- Every correction must be made in the owning layer and must not reintroduce service location, process leakage, broad runtime calls, or UI-derived authority.

## Safe cutover sequence

1. Freeze the candidate architecture and run all feature combinations and migration fixtures.
2. Use shadow comparison only for pure mapping/validation; never duplicate side-effecting execution.
3. Inject faults at context, authority, scope, provider, stream, tool, approval, persistence, finalizer, and process boundaries.
4. Triage by correlation IDs and fix the owning layer with a failing regression test first.
5. Remove temporary selectors only after rollback criteria and telemetry are satisfied.

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
