# SB07-service-locator-and-parallel-graph-removal: Remove service location, fallbacks, and mixed manual/DI runtime graphs

## Metadata

- Phase: B — Scope and construction integrity
- Depends on: `SB06-workspace-execution-scope-and-services-factory`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Make runtime construction explicit and deterministic by removing retained `IServiceProvider`, hidden `GetService` fallbacks, and the parallel manually constructed workspace graph.

## Why this subbundle exists

A factory does not solve architecture if runtime classes still reach back into the root container. Current manual workspace creation constructs some objects directly while passing the root provider to MAF for other dependencies.

## Scope

- Remove `IServiceProvider` fields from affected runtime/core collaborators.
- Replace hidden fallback construction with typed dependencies/catalogs/factories.
- Make `CanDoItAllAgentWorkspaceFactory` use one owned composition path.
- Add composition smoke and negative missing-registration tests.

## Non-goals

- No narrow runtime interface migration yet; SB09.
- No project-reference repair yet; SB12.

## Required SharedInfo skills

- `csharp-factory-builder-composition`
- `csharp-modular-refactoring`
- `csharp-testability-contracts`
- `csharp-architecture-review-gate`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Remove retained `IServiceProvider` from:
      - `MafAgentRuntime`;
      - `MafRuntimeAgentFactory`;
      - `RuntimeCapabilityComposer`;
      - `MafRuntimeDependencyResolver` (prefer deletion);
      - any newly extracted execution component.
2. Replace service lookups with typed dependencies:
      - provider runtime gateway/factory/gates;
      - credential service;
      - image analysis;
      - capability contributors/tool providers/context contributors;
      - logger/factory/telemetry options;
      - storage/spreadsheet/document services;
      - security secret runtime abstraction;
      - workspace runtime services factory.
3. Capture `IEnumerable<T>` contributions in a typed immutable catalog or pass them directly. Validate unique descriptors/IDs at construction. Do not make runtime code call `services.GetServices<T>()`.
4. Remove default singleton/fallback instances from production paths where missing registration would change behavior. Keep explicit test factories or Null Objects only when absence is a supported product capability and is visible in diagnostics.
5. Refactor `CanDoItAllAgentWorkspaceFactory.CreateWorkspaceService` to use one `IWorkspaceRuntimeScopeFactory`/owned composition. It must not manually build file/command/runtime services and then pass the root provider into MAF.
6. Define disposal ownership for workspace scope, runtime adapters, hosted agents, process host, and capability resources. Add concurrent/exceptional disposal tests.
7. Keep DI extension methods declarative. Do not call `BuildServiceProvider()` during registration. Split the giant module registration into cohesive extension methods only when this clarifies ownership; do not create file-only partial architecture.
8. Add source-architecture tests that reject `IServiceProvider` fields and runtime `GetService/GetRequiredService` calls outside approved composition files.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Only the application composition root/factory implementation may know the service container. Runtime, Core, MAF execution components, capability composers, and builders receive typed collaborators.

## Dependency Direction

Composition root depends on implementations. Runtime/core does not depend on composition. If an extraction creates a cycle, move a narrow contract inward rather than restoring service location.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Typed constructor injection, catalogs/contributor collections, and owned factories. `IServiceScopeFactory` is permitted only inside the outer scope factory implementation and must not escape.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Runtime components construct from explicit fakes without a DI container.
- Missing required registration fails in composition smoke with actionable type identity.
- Optional capability absence is explicit and does not create a fallback implementation.
- Workspace factory returns one owned graph and disposes it once.
- Root provider is not used after scope creation.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No `IServiceProvider` field in affected runtime/core types.
- No `GetService/GetServices/GetRequiredService` in runtime behavior except approved composition files.
- No `BuildServiceProvider` in registration.
- No production fallback `new` for required collaborators.

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

- `Architecture guard script.`
- `Focused constructor/composition/disposal tests.`
- `Release build and existing MAF architecture tests.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Runtime construction is explicit and testable without root DI.
- Manual and DI workspace paths converge on one factory.
- Missing dependencies fail fast.
- No hidden fallback changes runtime behavior.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Typed injection would require a forbidden inner-to-outer reference. Extract an abstraction first.
- A catalog is merely a wrapper over `IServiceProvider`. Redesign it as data/collaborator collection.
- Old manual graph remains active.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- explicit constructor graph
- typed catalogs/factories
- workspace composition migration
- source guard tests
- proof

## Downstream unlock

SB08 checkpoint may start when service-locator source assertions pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Manual workspace construction and root DI currently overlap; removing only one `GetService` call can leave two object graphs alive.
- Decorator order, singleton/scoped lifetime, disposal, provider credential scopes, scenario harnesses, and process mocks can change silently.
- Fallback `new Default...()` construction can mask missing registrations; replacing it requires fail-fast composition tests.
- Do not call `BuildServiceProvider` inside registration to make migration compile.

## Safe cutover sequence

1. Establish one typed composition root and explicit factories.
2. Move registrations/decorators before deleting manual constructors.
3. Migrate production entry points one family at a time and fail fast on missing dependencies.
4. Delete fallback construction only after composition smokes and disposal tests pass.

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
