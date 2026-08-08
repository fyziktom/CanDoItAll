# SB14-process-boundary-checkpoint: Checkpoint: MAF dependency direction and process ownership

## Metadata

- Phase: D — Dependency direction and process ownership
- Depends on: `SB12-maf-dependency-graph-repair`, `SB13-process-semantics-and-recovery-extraction`
- Checkpoint: Yes
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Block continuation/state/workflow cleanup unless MAF is product-neutral and process outcomes/recovery are proven to be owned by Processes.

## Why this subbundle exists

Later compatibility work would otherwise encode the wrong boundary into versioned state.

## Scope

- Execute CP4.
- Run dependency and process-symbol scans.
- Run process recovery/completion regression proof.
- Run architecture gate.

## Non-goals

- No new functionality beyond blocker fixes.

## Required SharedInfo skills

- `csharp-architecture-review-gate`
- `csharp-dependency-graph-audit`
- `canonical-model-review`
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

1. Run CP4.
2. Inspect `.csproj`, namespaces, process symbols, policy owners and recovery call path.
3. Run direct and integration tests.
4. Record downstream decision.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Checkpoint decision only.

## Dependency Direction

Prove MAF has no product module and Processes points inward only.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Architecture/dependency/canonical authority gate.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- All Phase D tests and relevant process regression suites.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No MAF `Modules.*`.
- No MAF process semantics.
- No recovery bypass.
- No dependency cycle.

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
- `CodeAnalytics dependency/cycle refresh.`
- `Architecture guard script.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- CP4 Unlocked.
- No Critical/High MAF/process boundary finding.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Any product module reference or process semantic remains in MAF.
- Recovered output bypasses canonical process completion.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- checkpoint result
- architecture gate
- dependency/process proof

## Downstream unlock

Only `Unlocked` permits SB15.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Calculator-only success is insufficient; include repair, approval, blocked, Tetris/multi-step, subprocess, stale-artifact, and provider-failure paths.
- Verify MAF contains no process source strings, outcome types, artifact path conventions, or provider override branches hidden behind renamed helpers.

## Safe cutover sequence

1. Run source/dependency scans and process integration matrices.
2. Inspect persisted receipts/artifacts to prove no bypass of completion gates.
3. Disable recovery and verify safe failure as the rollback behavior.
4. Unlock only when MAF is process-agnostic.

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
