# SB05-context-foundation-checkpoint: Checkpoint: context, affinity, Gantt, and authority foundation

## Metadata

- Phase: A — Evidence and context foundation
- Depends on: `SB03-floating-conversation-affinity-and-transitions`, `SB04-project-structure-gantt-observation`
- Checkpoint: Yes
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Block further work unless the floating-agent context model is coherent, practical scenarios pass, and UI observation is proven separate from execution authority.

## Why this subbundle exists

Later workspace scope and runtime refactors will depend on these records. Continuing with ambiguous context ownership would merely move the ambiguity into new factories and ports.

## Scope

- Run canonical-model review and C# architecture gate.
- Execute CP1 from `plan/architecture-checkpoints.md`.
- Inspect old/new context paths and remove duplicate production behavior.
- Record `Unlocked`, `Blocked`, or bounded follow-up.

## Non-goals

- No new feature implementation beyond narrowly fixing blockers discovered by the gate.

## Required SharedInfo skills

- `canonical-model-review`
- `csharp-architecture-review-gate`
- `candoitall-csharp-architecture-bundle-guard`
- `csharp-dependency-graph-audit`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Run the canonical model classification using `reviews/canonical-model-review.md`.
2. Run every floating context/authority/Gantt scenario in `plan/validation-matrix.md` applicable to Phase A.
3. Inspect context records for overloading and accidental persistence of UI attachments.
4. Inspect production caller graph: V2 turn capture must be authoritative; V1 may be a thin adapter only.
5. Inspect transition and epoch behavior for same-project and cross-project cases.
6. Inspect UI for context badge/detach behavior and no navigation-triggered provider call.
7. Run dependency/cycle audit and source assertions.
8. Complete `reviews/csharp-architecture-gate.md` and a checkpoint result.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

The checkpoint owns no runtime behavior. It owns the decision whether the context foundation is trustworthy enough for dependent work.

## Dependency Direction

Confirm that context contracts remain SDK/product neutral and that product contributors depend inward. No Core -> Workbench/UI reference is allowed.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Architecture gate. Reject “pass because tests compile”; require behavior, negative authority tests, source assertions, and owner shrink proof.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- All Phase A tests plus regression floating chat/activity tests.
- Negative authority tests are mandatory.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No direct UI scope-to-runtime authority path.
- No duplicate old/new Send production path.
- No full UI attachment persisted as chat/execution canonical state.

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

- `Release build of affected projects/solution.`
- `Targeted unit/component/integration tests.`
- `Bundle proof validator.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- CP1 status is `Unlocked` or `Unlocked with bounded follow-up`.
- No Critical/High context/source-of-truth finding remains.
- All practical user scenarios are demonstrated.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Any context or authority finding is Critical/High.
- A running/approval turn can be retargeted after navigation.
- A project switch does not resolve new authority.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- checkpoint result
- canonical model review
- architecture gate
- updated proof manifest

## Downstream unlock

Only an explicit `Unlocked` decision allows SB06.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- A green unit suite is insufficient if rapid navigation, multiple floating chats, profile switching, or approval continuation are untested.
- Context snapshots must prove atomic old-or-new publication, not mixtures of fragments and attachments.
- Transition text supplied to the model must be application-generated and bounded, while UI content remains untrusted data.

## Safe cutover sequence

1. Run the complete context scenario matrix and race tests.
2. Inspect persisted invocation metadata and turn references for raw UI authority leakage.
3. Unlock only after Canvas -> Gantt, Project X -> Y, rapid navigation, profile switch, multiple chats, and approval retention pass.

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
