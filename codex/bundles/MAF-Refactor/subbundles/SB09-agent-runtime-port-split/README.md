# SB09-agent-runtime-port-split: Split the broad agent runtime into SDK-free narrow ports

## Metadata

- Phase: C — Runtime port split
- Depends on: `SB08-scope-and-composition-checkpoint`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Create runtime-neutral narrow agent/runtime interfaces and migrate application callers so execution, continuation, diagnostics, provider model administration, and hosted agent creation no longer share one broad contract. Explicitly remove ordinary LLM invocation from this contract family and reserve its separate provider-backed boundary for SB16.

## Why this subbundle exists

The current interface carries unrelated operations and long parameter lists. It prevents independent tests and forces ordinary callers to depend on session/tool/finalizer concerns they do not use.

## Scope

- Create/confirm `AgentFramework.Runtime.Abstractions`.
- Define request/response/failure contracts.
- Migrate Core and provider diagnostics callers to narrow ports.
- Add a temporary delegating compatibility facade with no new callers.

## Non-goals

- Do not yet fully decompose internal MAF implementation; SB10.
- Do not remove product references from MAF yet; SB12.
- Do not migrate workflow LLM caller yet; SB16.

## Required SharedInfo skills

- `csharp-project-boundary-extraction`
- `csharp-dependency-graph-audit`
- `csharp-testability-contracts`
- `csharp-architecture-governor`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Create interfaces:
      - `IAgentExecutionRuntime.ExecuteAsync(AgentExecutionRuntimeRequest, ...)`;
      - `IAgentContinuationRuntime.ContinueAsync(AgentContinuationRuntimeRequest, ...)`;
      - `IProviderDiagnosticsRuntime`;
      - `IProviderModelAdministrationRuntime`;
      - `IHostedAgentFactory` only for true hosted/A2A use;
      - do not add `ILlmInvocationPort` to Runtime.Abstractions; record the separate `Llm.Abstractions` boundary for SB16.
2. Define request records that include explicit provider/agent/capability snapshots, turn context reference/lease, authority/scope services, output contract, progress observer, and cancellation. Keep application persistence and UI types out.
3. Define runtime-neutral result/failure records. Preserve response text, usage observations, tool/finalizer traces, pending approval proposals, runtime state envelope, context diagnostics and provider compatibility evidence.
4. Replace the single continuation boolean with a stable-ID decision collection in the new contract. Keep compatibility mapping outside the new port.
5. Migrate `AgentFrameworkWorkspaceExecutionService` to depend on execution and continuation ports. Migrate provider diagnostics/catalog services to their ports. Identify hosted-agent callers and migrate them deliberately.
6. Create a temporary broad facade adapter that delegates to narrow ports. Mark obsolete/internal where feasible. Add an architecture test preventing new production references.
7. Move contracts out of overloaded `Contracts.cs` into focused files/projects. Keep backward serialization compatibility for result records where needed.
8. Add direct fake-port tests for Core execution coordination. Tests must not instantiate MAF or the old broad facade.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Runtime.Abstractions owns SDK-free agent execution/continuation/diagnostic/administration ports. Core/application owns execution coordination. MAF later implements those ports. The lightweight LLM port belongs to separate `Llm.Abstractions` and is implemented over provider runtime in SB16. Provider profile/product persistence remain outside both contract projects.

## Dependency Direction

Runtime.Abstractions -> Models only (plus narrowly justified low-level abstractions). Core -> Runtime.Abstractions. MAF -> Runtime.Abstractions. No Runtime.Abstractions -> Core/MAF/Modules/UI/SDK. No lightweight LLM contract is added here merely to avoid the separate SB16 boundary.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Ports and adapters with command records. Replace long parameter lists with immutable request objects. Avoid a single `IAgentRuntimeV2` that merely renames the old interface.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Core execution coordinator calls execution port with the expected immutable request.
- Continuation coordinator maps stable approval decisions and original context/authority.
- Diagnostics and model administration resolve independently.
- Runtime contract assembly has no SDK/product/UI references.
- Compatibility facade delegates exactly and does not add behavior.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No new caller of broad `IAgentRuntime`.
- Runtime.Abstractions project is SDK-free.
- Core execution tests use fake narrow ports.

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

- `Build Runtime.Abstractions, Core, MAF, Hosting, modules.`
- `Focused runtime-port tests.`
- `Dependency graph/cycle audit.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Agent execution, continuation, diagnostics, administration, and hosted-agent callers use their narrow ports; ordinary LLM callers are explicitly excluded and reserved for SB16.
- Broad facade is compatibility-only.
- Contracts are SDK-free and independently testable.
- No new cycle or implementation leak.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- A port includes MAF SDK types. Map them inside the adapter.
- A request record becomes another universal context bag. Split execution, continuation, diagnostics.
- A production caller continues to bypass the ports.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- Runtime.Abstractions project/contracts
- migrated Core callers
- compatibility facade
- tests/dependency proof

## Downstream unlock

SB10 may start when every production caller that truly requires agent/runtime behavior enters the appropriate narrow port and every ordinary LLM caller is inventoried/blocked from adopting those ports pending SB16.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Production callers span Core execution, workspace services, provider diagnostics/model administration, hosted/A2A paths, scheduler wiring, test hosts, and runtime decorators. Workflow LLM is inventoried and guarded here but migrates only in SB16 through a separate LLM boundary.
- Request/result contracts must retain usage observations, tool/finalizer traces, compatibility evidence, progress, cancellation, runtime state, and pending approvals without importing MAF SDK types.
- Mocks such as `ProcessMockAgentRuntime` and `ScenarioHarnessAgentRuntime` can accidentally become implementations of every new interface and recreate the broad facade.
- Per-proposal continuation must not be reduced back to one boolean inside the new port.

## Safe cutover sequence

1. Add narrow contracts and adapters while the broad facade remains the single compatibility entry.
2. Migrate diagnostics/model administration first because they are tool/session-free.
3. Migrate execution and continuation coordinators with differential fake-port tests.
4. Migrate hosted/A2A, mock, harness, and test-host wiring deliberately.
5. Add a guard that forbids new broad-runtime callers before continuing.

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
