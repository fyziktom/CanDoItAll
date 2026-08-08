# SB06-workspace-execution-scope-and-services-factory: Workspace execution scope and scope-bound services factory

## Metadata

- Phase: B — Scope and construction integrity
- Depends on: `SB05-context-foundation-checkpoint`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Create one immutable execution-scope identity and one owned factory-produced service bundle so every file, command, artifact, MCP, path, receipt, and process-host operation in a turn uses the same workspace boundary.

## Why this subbundle exists

The current composer combines constructor scope, per-run context scope, and services that may already be bound in DI. This can produce a run whose policy claims one scope while a concrete service uses another.

## Scope

- Introduce `WorkspaceExecutionScope`, `WorkspaceRuntimeServices`, and factory/lease interfaces.
- Migrate capability composition to consume the bundle.
- Make authority snapshot the sole source of workspace execution scope.
- Validate scope identity through all relevant tool builders/plugins.

## Non-goals

- Do not yet remove every `IServiceProvider` from all MAF classes; SB07 completes that work.
- Do not split the broad runtime port yet.

## Required SharedInfo skills

- `csharp-factory-builder-composition`
- `csharp-project-boundary-extraction`
- `csharp-testability-contracts`
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

1. Define `WorkspaceExecutionScope` with root, scope descriptor, database profile ID/generation, authority ID/fingerprint, and optional execution run ID. Validate normalization and identity equality.
2. Define `WorkspaceRuntimeServices` as a cohesive owned bundle. Include every scope-bound dependency currently independently resolved or constructed by runtime capability composition. Give the bundle an immutable identity and explicit disposal ownership.
3. Implement `IWorkspaceRuntimeServicesFactory.CreateAsync(scope, ...)`. The implementation may use an owned DI scope internally, but returns typed services and must not leak `IServiceProvider`.
4. Replace `MafRuntimeDependencyResolver.ResolveWorkspaceServices` in production runtime construction. Missing services fail with a named composition exception; no fallback `new WorkspaceFileService(...)` path remains.
5. Change `RuntimeCapabilityComposer`/builders to receive the bundle for the current run. Remove use of constructor `workspaceScope` for per-run plugins. Ensure MCP, browser artifact paths, spreadsheet, storage, skills/scripts, file and command tools receive the same effective scope identity.
6. Connect the factory input to the canonical `AgentExecutionAuthoritySnapshot`, not directly to `AgentUiObservationSnapshot` or payload data.
7. Add runtime assertions in development/test builds that a builder/plugin service identity equals the execution scope. Use typed identity rather than comparing path strings only.
8. Define bundle lifetime: creation before capability/tool assembly, disposal after runtime build/turn and after any retained tool lifetime is complete. Preserve existing process lease cleanup behavior.
9. Update manual workspace creation paths to request a bundle from the same factory or prepare a migration adapter for SB07. Do not maintain two different construction algorithms.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Core/runtime-neutral workspace contracts own scope identity. An outer infrastructure/composition factory owns construction and disposal. Capability/tool builders consume one bundle and do not independently resolve services.

## Dependency Direction

The factory implementation may reference concrete workspace services. Core and MAF receive the abstraction/bundle. Do not make Core reference the AgentFramework module or product modules.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Abstract Factory plus owned lease. Construction is explicit and fail-fast. Reject a dictionary/service-provider wrapper disguised as a factory.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Project, Organization, and Sandbox factories create distinct and correct identities.
- All services in one bundle match the same identity.
- Mismatched pre-bound service is rejected.
- Missing required service fails fast; no fallback.
- MCP/browser artifact path and receipt writers use the same project scope as file/command services.
- UI observation claiming a different scope cannot change factory input.
- Owned bundle disposes exactly once and preserves primary failure.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No production call to `ResolveWorkspaceServices` fallback path.
- No per-run plugin receives constructor scope when authority scope is available.
- Factory result contains no raw `IServiceProvider` property.

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

- `Focused Core/MAF workspace tests.`
- `Existing workspace file/command/artifact/MCP tests.`
- `Integration scope isolation tests.`
- `Affected project and solution builds.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- One execution has one workspace scope identity across all paths.
- Scope comes only from canonical authority.
- Missing/mismatched services fail before provider/tool execution.
- Old fallback path is removed or unreachable with deletion scheduled in SB07.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- A required service cannot expose/validate scope identity. Add a narrow identity contract; do not infer from global state.
- Factory construction creates a Core -> Module dependency. Move implementation outward.
- Two active construction algorithms remain.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- scope contracts
- services bundle/factory
- migrated capability composition
- scope tests/proof

## Downstream unlock

SB07 may start after scope identity tests pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- This is a critical authorization boundary. Mixing one legacy service with one new scope-bound plugin in a single run is forbidden.
- File, command, artifact, process-host, image/document, receipt, MCP/browser path, and external-target services must expose the same scope identity.
- Organization and Project scopes can share physical roots; path equality is not sufficient proof of authority equality.
- Disposal ownership and kept-alive process leases must remain correct when the service bundle becomes owned per execution.

## Safe cutover sequence

1. Create and test the complete scope-bound service bundle without changing callers.
2. Add an identity assertion/decorator to every service and tool boundary.
3. Switch a whole execution to the new bundle in one composition decision; never mix legacy/new services in one run.
4. Retain a whole-run rollback selector until SB08; log selected bundle version and scope identity.

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
