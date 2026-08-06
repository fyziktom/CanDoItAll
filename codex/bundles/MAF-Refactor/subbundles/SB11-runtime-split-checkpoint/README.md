# SB11-runtime-split-checkpoint: Checkpoint: runtime ports and MAF adapter decomposition

## Metadata

- Phase: C — Runtime port split
- Depends on: `SB09-agent-runtime-port-split`, `SB10-maf-adapter-decomposition`
- Checkpoint: Yes
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Block dependency/process work unless the runtime split is real, SDK boundaries are clean, and extracted behavior is directly testable.

## Why this subbundle exists

A facade that delegates to another monolith or tests that still instantiate the old runtime would be fake separation.

## Scope

- Execute CP3.
- Run architecture gate, dependency audit, runtime proof slices, and old-owner source assertions.

## Non-goals

- No product reference/process recovery repair except blockers.

## Required SharedInfo skills

- `csharp-architecture-review-gate`
- `csharp-testability-contracts`
- `csharp-dependency-graph-audit`
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

1. Run CP3 checklist.
2. Inspect caller graph for broad runtime.
3. Inspect extracted owners and tests.
4. Run MAF proof slices and composition tests.
5. Record downstream decision.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Checkpoint decision only.

## Dependency Direction

Confirm Runtime.Abstractions is SDK-free and Core callers do not reference MAF.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Architecture review gate.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- All Phase C tests and existing MAF runtime/approval/handoff/finalizer tests.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- Broad facade delegation-only.
- No new broad manager/partial.
- No SDK types in runtime contracts.

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
- `Architecture guard scripts.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- CP3 Unlocked.
- No Critical/High runtime split/testability finding.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Broad facade still owns algorithms or production callers bypass ports.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- checkpoint result
- architecture gate
- runtime proof manifest

## Downstream unlock

Only `Unlocked` permits SB12.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Search for broad-runtime usage in source, Razor, DI factories, tests that model production registration, and reflection/source assertions.
- Prove that direct unit tests instantiate each extracted adapter without constructing the old facade.
- Verify provider health/model administration do not transit the agent execution graph.

## Safe cutover sequence

1. Build the whole solution and run broad-caller/source scans.
2. Compare response/usage/tool/finalizer/session fixtures against SB00.
3. Exercise provider errors, cancellation, background/streaming, and disposal fault injection.
4. Unlock only when the old facade has no business behavior.

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
