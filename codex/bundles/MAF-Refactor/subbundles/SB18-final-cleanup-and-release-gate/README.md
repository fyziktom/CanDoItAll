# SB18-final-cleanup-and-release-gate: Final cleanup, deletion, full validation, and release architecture gate

## Metadata

- Phase: F — Cross-cutting stabilization and release
- Depends on: `SB17-cross-cutting-cutover-stabilization-and-bugfixing`
- Checkpoint: Yes
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Delete migration scaffolding and obsolete paths, run full architecture/canonical/dependency/test gates, and leave one production path per responsibility.

## Why this subbundle exists

Temporary facades, adapters, legacy metadata readers and flags are useful only when they have an enforced removal point. Without deletion, the architecture will drift back to multiple authorities.

## Scope

- Delete broad runtime facade/legacy APIs when caller scan is empty.
- Delete fallback resolver and duplicate context/recovery paths.
- Remove migration flags and obsolete types where safe.
- Update docs and architecture tests.
- Run CP6 and full validation after CP5 cleanup readiness.

## Non-goals

- No unrelated feature work.
- No new architecture layer added to avoid deleting old code.

## Required SharedInfo skills

- `csharp-architecture-review-gate`
- `canonical-model-review`
- `csharp-dependency-graph-audit`
- `csharp-testability-contracts`
- `candoitall-csharp-architecture-bundle-guard`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify SB17 recorded `Ready for cleanup` or `Ready with named compatibility readers retained`.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Run production caller scans for:
      - broad `IAgentRuntime`/`MafAgentRuntime`;
      - V1 context invocation authority mapping;
      - bool-all approval compatibility API;
      - unversioned session-state writes;
      - `MafRuntimeDependencyResolver` fallbacks;
      - old Project Structure context builder paths;
      - process recovery in MAF/generic branches;
      - workflow full-agent LLM path;
      - lightweight LLM abstractions coupled to agent/MAF/provider SDK/UI/workspace/process types;
      - duplicate provider credentials/HTTP/retry/usage code in the lightweight implementation;
      - mock, scenario, diagnostic, API-test-host, scheduler, hosted/A2A, and manual factory callers of obsolete seams.
2. Delete obsolete facades, methods, properties, files, registrations, project references, flags and tests that only validate removed behavior. Keep explicit legacy readers only when persisted production data still requires them; document removal criteria.
3. Ensure old large owners are thin or gone. Record before/after responsibility and line/caller counts, but prioritize ownership proof over line count.
4. Update repository architecture documentation:
      - floating context/epoch/turn semantics;
      - observation versus authority;
      - workspace scope bundle;
      - runtime ports and MAF adapter;
      - process policy ownership;
      - runtime-state envelope and continuation;
      - obsolete full-agent workflow LLM path;
      - provider-backed lightweight LLM boundary and future ordinary-chat source-of-truth rules.
5. Run the bundle architecture guard, dependency report, forbidden-pattern scan, canonical-model review, and C# architecture gate.
6. Run full Release build and relevant Unit, Components and Integration suites. Run any live provider/process smoke only when credentials/environment are intentionally available; do not make them the sole proof.
7. Review telemetry/privacy: no raw context attachments, prompts, secrets or sensitive tool arguments were introduced. Verify bounded context and cache behavior.
8. Complete final proof manifest with exact commits, commands, failures/fixes, dependency graph, source assertions and unresolved bounded follow-ups.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Final gate owns closure evidence. Production ownership must match ADRs 001–011 with no duplicate path.

## Dependency Direction

Final dependency graph must match `architecture/02-csharp-dependency-direction.md` or a reviewed ADR amendment. MAF has no product references; Core has no MAF/product/UI references.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Strangler completion and deletion. Reject leaving compatibility facades “for safety” without callers.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Full relevant Unit suite.
- Full relevant Components suite.
- Full relevant Integration suite.
- Architecture/source assertion suite.
- Practical Canvas/Gantt/cross-project/approval scenarios.
- Runtime-state migration and workflow direct-port regressions.
- Provider-backed lightweight invocation parity, exact-once driver/usage, streaming terminal semantics, and no-agent/no-authority tests.
- Mock/harness/diagnostic/API-host composition parity and guard self-tests.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No broad runtime production caller/facade unless an approved persisted-data compatibility reader only.
- No MAF product/process dependency.
- No service locator/fallback.
- No UI observation authority path.
- No workflow payload authority path.
- No duplicate old/new production execution.
- `Llm.Abstractions` is SDK-free, agent-free, workspace/authority-free, process-free, and UI-free.
- Ordinary workflow/lightweight paths reach the existing provider runtime exactly once and never construct an agent/session/capability graph.
- Every mock/harness/diagnostic/test host uses the accepted production contracts rather than a private legacy graph.

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

- ``python <bundle>/scripts/validate_bundle_structure.py <bundle>``
- ``python <bundle>/scripts/check_architecture_guards.py --repo-root .``
- ``python <bundle>/scripts/report_project_references.py --repo-root .``
- `Release solution build and test commands from validation matrix.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- CP5 cleanup readiness, CP6, and both architecture reviews pass.
- No Critical/High finding remains.
- One owner and one production path exist per concern.
- All required tests/builds pass or an explicit external-environment-only gap is documented without weakening architecture proof.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Any compatibility path has an active caller and no owner/removal plan.
- Any authority, scope, dependency or process boundary is unresolved.
- Full test failure indicates behavior regression.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- final checkpoint result
- canonical model review
- C# architecture gate
- full proof manifest
- updated docs
- deletion/source proof

## Downstream unlock

Bundle closes only with `Pass` and `Unlocked` final status.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Deleting compatibility readers too early can strand persisted waiting approvals or historical runs.
- Leaving feature flags/facades indefinitely recreates architectural ambiguity.
- Public API projections must not expose runtime envelopes, provider IDs, authority payloads, raw attachments, or approval internals.
- Source assertions and dependency gates must run after the last deletion, not only before cleanup.

## Safe cutover sequence

1. Confirm no waiting legacy state would be stranded by each deletion.
2. Remove broad facades, obsolete writers, flags, selectors, and duplicate adapters in small buildable commits.
3. Re-run all architecture/source/dependency/public-projection guards after deletion.
4. Run the full Release build/test matrix and manual Canvas/Gantt/process/workflow/lightweight-LLM acceptance.

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
