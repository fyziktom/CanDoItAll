# SB00-current-state-characterization: Current-state characterization and dependency baseline

## Metadata

- Phase: A — Evidence and context foundation
- Depends on: None
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Freeze the observable behavior and compile-time dependency baseline before moving any responsibility. Produce evidence that can distinguish a safe refactor from a behavior rewrite.

## Why this subbundle exists

The current floating context path already has valuable invariants: atomic publication, navigation fences, immutable per-turn capture, digest binding, and original-context approval continuation. Those must be proven before extraction. The architecture also has hidden service-location, scope, and project-reference behavior that must be measured rather than guessed.

## Scope

- Verify the target branch and baseline commit relationship.
- Create a CodeAnalytics snapshot when available and record solution/project dependency evidence.
- Map all production callers of the broad `IAgentRuntime`, `MafAgentRuntime`, `RuntimeCapabilityComposer`, `MafRuntimeDependencyResolver`, floating context registry, process artifact recovery, provider chat-completion drivers/runtime pool, and workflow LLM path.
- Record constructor dependencies, retained fields, line counts, direct `new` paths, `IServiceProvider` usage, assembly references, and test ownership.
- Add characterization tests only where existing behavior is not already locked.

## Non-goals

- No production architecture extraction.
- No interface rename or project move.
- No change to floating chat semantics, approval semantics, provider behavior, or process completion.

## Required SharedInfo skills

- `csharp-architecture-governor`
- `csharp-dependency-graph-audit`
- `csharp-testability-contracts`
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

1. Record repository state:
      - branch, HEAD, dirty files, solution file, SDK version;
      - baseline ancestor check against `51d9a2f071e9a5f295abac884c8c667328462cc4`;
      - SharedInfo skill availability and versions.
2. Build an affected-project table from `.csproj` files. Include direct and transitive references for Models, Core, Hosting, MAF, Workflow MAF adapter, AgentFramework module, Workbench, Processes, and Security.
3. Use CodeAnalytics MCP when available:
      - create a scoped snapshot;
      - run solution inventory and dependency/cycle queries;
      - locate all callers of `IAgentRuntime.RunAsync`, `RespondToPendingApprovalsAsync`, provider diagnostics methods, and `MafAgentRuntime.CreateHostedAgentAsync`;
      - record hotspots and exact symbols.
4. Create a responsibility and construction inventory for the classes named in `architecture/00-csharp-current-state-inventory.md`. Include constructor parameter count, fields, `IServiceProvider` access, direct instantiation, and disposal ownership.
5. Characterize floating context behavior:
      - active Project Structure view republishes Canvas/Gantt distinctions;
      - strict capture is atomic and route-fenced;
      - a captured turn remains immutable after navigation;
      - continuation resolves the original transient context and fails if unavailable;
      - navigation alone does not execute the provider.
6. Characterize current Gantt context limitations. Assert that the parent context identifies Gantt, while rich projection facts are not contributed by `ProjectStructureGanttPanel`. This test may be a source/contract assertion rather than an intentionally failing runtime test.
7. Characterize current scope construction with targeted tests or probes. Record whether file, command, artifact, MCP, and receipt services can be resolved from different scope origins. Do not fix it in this subbundle.
8. Characterize the provider-neutral lightweight-inference foundation:
      - exact `IProviderChatCompletionDriver` implementations and request/result contracts;
      - runtime pool/handle, dispatch-lane, credential, retry, blank-response, model-normalization, attachment, response-format, streaming, and usage ownership;
      - current `MafProviderRuntimeGateway` callers and responsibilities;
      - missing semantics that SB16 must add without creating a parallel provider stack.
9. Execute `scan_affected_runtime_callers.py` and retain its JSON output as baseline evidence. Manually supplement Razor/generated/registration callers that a token scan or CodeAnalytics misses.
10. Run targeted existing tests, then a clean Release build. Store exact commands and transcripts in `proof/`.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

This subbundle changes no ownership. It documents current ownership and ambiguity. The evidence folder becomes the baseline authority for later before/after claims.

## Dependency Direction

Do not add project references. Record the current graph, including `AgentFramework.Maf -> Modules.Security`, `AgentFramework.Maf -> Modules.Workspace`, and `AgentFramework.Maf -> Workflows.MafAdapter`. Record cycles and transitive SDK/product leakage.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Characterization testing and architecture inventory. Reject speculative cleanup: every later extraction must cite a baseline responsibility and observable test.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Existing `FloatingAgentChatArchitectureTests` and related component tests.
- Existing MAF architecture, approval round-trip, handoff, finalizer, process integration and workflow adapter tests.
- New characterization test for Canvas capture -> UI Gantt switch -> original capture unchanged.
- New characterization/source assertion for no provider invocation on navigation.
- Provider runtime/driver characterization for test chat and direct completion, including invocation count, usage, failure, cancellation, and lifetime ownership.
- Workflow LLM characterization for text, JSON/schema, provider selection, usage projection, failure, cancellation, and current payload-scope inference.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- List every `IServiceProvider` field under affected runtime/core types.
- List every `ProjectReference` from MAF to `Modules.*` and the workflow adapter.
- List every process-specific symbol/string in the MAF project.
- List every broad runtime production caller.
- List provider runtime/driver/lightweight-inference candidates and owners.
- List every manual construction, mock/harness, diagnostic, and API-test-host path.

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

- ``dotnet build CanDoItAll.slnx --configuration Release``
- `Targeted Unit, Components, and Integration filters named in the proof manifest.`
- ``python <bundle>/scripts/report_project_references.py --repo-root . --json-out <proof>/project-references.json``
- ``python <bundle>/scripts/scan_affected_runtime_callers.py --repo-root . --json-out <proof>/affected-callers.json``

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Baseline proof manifest is complete and reproducible.
- All existing relevant tests pass, or pre-existing failures are isolated with evidence.
- No production behavior has changed.
- Every later subbundle has a confirmed caller/dependency inventory to work from.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- The target branch no longer resembles the analyzed architecture enough to trust this bundle.
- CodeAnalytics or direct source inspection reveals a dependency cycle not represented in the boundary plan.
- Characterization reveals that approval continuation currently recaptures UI context or otherwise contradicts a required invariant; repair the plan before continuing.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- `proof/baseline-proof-manifest.json`
- before dependency graph
- responsibility inventory
- test transcripts
- checkpoint recommendation for SB01

## Downstream unlock

SB01 may start only after the baseline proof is reviewed and recorded as complete.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Hidden broad-runtime callers may exist in Razor code, DI extension methods, mock/harness decorators, API test hosts, scheduler registration, and source-based architecture tests.
- Characterization that covers only `MafAgentRuntime` will miss orchestration behavior in `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Existing waiting approvals and serialized runtime state are migration fixtures, not disposable test data.
- CodeAnalytics may omit generated/Razor call sites; direct repository search and `.csproj` inspection remain mandatory.

## Safe cutover sequence

1. Make no production cutover. Add characterization tests and inventories only.
2. Capture before-state project references, exact broad-runtime callers, DI registrations, persisted state fields, tool manifests, and representative context snapshots.
3. Freeze golden/fake-provider fixtures for send, continuation, process recovery, workflow LLM, and provider diagnostics.
4. Record known failing tests separately from regressions introduced later.

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
