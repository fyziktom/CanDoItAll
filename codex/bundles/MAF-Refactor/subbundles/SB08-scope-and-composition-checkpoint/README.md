# SB08-scope-and-composition-checkpoint: Checkpoint: scope identity and composition integrity

## Metadata

- Phase: B — Scope and construction integrity
- Depends on: `SB06-workspace-execution-scope-and-services-factory`, `SB07-service-locator-and-parallel-graph-removal`
- Checkpoint: Yes
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Block runtime-port work unless workspace scope and construction are deterministic, explicit, and independently testable.

## Why this subbundle exists

Splitting interfaces on top of a mixed-scope service locator would create attractive abstractions over unsafe construction.

## Scope

- Execute CP2.
- Run architecture review gate and dependency audit.
- Prove scope identity through all tool paths.
- Prove absence of runtime service location/fallbacks.

## Non-goals

- No runtime interface or MAF project-reference changes except blocker repair.

## Required SharedInfo skills

- `csharp-architecture-review-gate`
- `csharp-dependency-graph-audit`
- `csharp-testability-contracts`
- `candoitall-csharp-architecture-bundle-guard`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Run CP2 checklist.
2. Run architecture guard script.
3. Inspect all scope-bound service identities and disposal ownership.
4. Run unit/composition/integration tests.
5. Record blockers/follow-ups and downstream decision.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Checkpoint decision only.

## Dependency Direction

Confirm composition depends inward and no Core/module cycle was added.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Architecture and construction gate.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- All Phase B tests.
- Organization/Project/Sandbox isolation.
- Negative missing/mismatch service tests.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No affected runtime/core `IServiceProvider` field.
- No fallback workspace construction.
- No parallel manual/root-container graph.

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

- `Release solution build.`
- `Targeted and relevant regression tests.`
- `CodeAnalytics dependency/cycle refresh.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- CP2 is explicitly Unlocked.
- No Critical/High scope or construction finding remains.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Any scope identity mismatch or hidden service location remains.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- checkpoint result
- architecture gate
- dependency proof
- test/build transcripts

## Downstream unlock

Only `Unlocked` permits SB09.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Scope identity must be proven through actual tool invocation paths, not only factory return values.
- Profile switching and concurrent Project X/Project Y executions must not reuse scoped services or runtime state.
- Cleanup failures must preserve the primary execution failure and release all owned resources in the established order.

## Safe cutover sequence

1. Run parallel and profile-switch executions with distinct scope IDs.
2. Fault cleanup/disposal and confirm primary-error preservation.
3. Remove the legacy whole-run scope selector only after all production entries use the new bundle.

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
