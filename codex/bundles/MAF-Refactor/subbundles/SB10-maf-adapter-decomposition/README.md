# SB10-maf-adapter-decomposition: Decompose MAF implementation behind the narrow ports

## Metadata

- Phase: C — Runtime port split
- Depends on: `SB09-agent-runtime-port-split`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Implement the narrow ports with cohesive MAF adapter components and reduce `MafAgentRuntime`, `MafRuntimeAgentFactory`, and capability composition to thin, explicit collaborators.

## Why this subbundle exists

Moving interface declarations does not fix a distributed God Object. MAF-specific streaming, continuation, session, finalizer, diagnostics, build, and response mapping need independent owners and direct tests.

## Scope

- Add MAF execution/continuation/diagnostics/administration/hosting adapters.
- Extract streaming-turn execution and response mapping.
- Keep generic finalizer protocol mechanics but remove application recovery semantics.
- Use typed capability contributions and the scope-bound services bundle.
- Make tool collisions fail globally.

## Non-goals

- Process recovery migration completes in SB13.
- Project references are repaired in SB12.
- Legacy runtime-state envelope migration completes in SB15.

## Required SharedInfo skills

- `csharp-modular-refactoring`
- `csharp-provider-tool-plugin-isolation`
- `csharp-factory-builder-composition`
- `csharp-testability-contracts`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Create cohesive implementations:
      - `MafAgentExecutionAdapter`;
      - `MafAgentContinuationAdapter`;
      - `MafProviderDiagnosticsAdapter`;
      - `MafProviderModelAdministrationAdapter`;
      - `MafHostedAgentFactory` when required;
      - `MafStreamingTurnExecutor`;
      - `MafRuntimeResponseMapper`;
      - focused generic finalizer protocol/recovery components;
      - state adapter placeholder for SB15.
2. Refactor `MafRuntimeAgentFactory` into a narrow runtime-build factory. Move policy middleware, capability assembly, handoff build, and provider agent construction into explicit collaborators with one reason to change.
3. Refactor `RuntimeCapabilityComposer` around registered contribution descriptors/catalogs and the per-run `WorkspaceRuntimeServices` bundle. It must not know every concrete product provider. Preserve deterministic order and diagnostics.
4. Replace global `DeduplicateTools(... first())` with explicit global duplicate validation. A duplicate name/provider identity is a composition error unless an explicit override policy with one owner exists.
5. Keep approval protocol mapping in the continuation adapter and stable-ID mapper. Application approval authority remains outside MAF.
6. Keep MAF required-finalizer tool mechanics generic. When finalizer repair is exhausted, return typed failure/partial evidence to the application recovery pipeline; do not synthesize process outcomes.
7. Make `MafAgentRuntime` a small delegating compatibility facade. It must contain no streaming loop, session algorithm, finalizer repair algorithm, service lookup, or product branch.
8. Preserve disposal ordering and primary-failure behavior in owned runtime build results. Add direct tests to extracted owners.
9. Preserve telemetry/usage/tool traces while ensuring sensitive data stays disabled/redacted.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

MAF adapter components own MAF SDK interaction only. Capability contributors own tool/context contributions. Application Core owns run persistence, authority, and recovery coordination.

## Dependency Direction

MAF implements Runtime.Abstractions and may reference runtime-neutral Core tool/workspace abstractions. It must not introduce new product references. Adapter components must not be referenced by Core.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Anti-corruption adapter plus cohesive strategy/services. Use a runtime build factory for construction and a streaming turn executor for one provider turn. Reject splitting by arbitrary file size or adding nested/partial types.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Execution adapter mapping and streaming completion.
- Continuation adapter stable-ID approval mapping.
- Diagnostics and model administration independent tests.
- Finalizer success, missing finalizer typed failure, JSON repair, and provider failure paths.
- Global duplicate tool collision fails.
- Runtime build disposal order/concurrency/primary failure.
- Capability contributor order/filtering with fake contributors.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- `MafAgentRuntime.cs` is delegation-only and bounded in size/responsibility.
- No partial runtime files.
- Extracted tests instantiate extracted components directly.
- No silent first-wins tool deduplication.

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

- `Existing MAF runtime proof slices.`
- `Targeted new adapter/component tests.`
- `Release build and architecture scans.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Narrow ports have real MAF implementations.
- Old facade owns no algorithms.
- Capability composition is contributor-based and scope-bound.
- Extracted behavior has direct positive and negative tests.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Behavior is copied rather than moved, leaving two active algorithms.
- A new broad `MafRuntimeManager` replaces the old facade.
- Process-specific fallback is retained as “temporary” without SB13 hook.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- MAF adapter components
- thin compatibility facade
- direct tests
- tool collision policy
- proof

## Downstream unlock

SB11 checkpoint may start after source assertions and runtime proof slices pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Streaming update order, provider blank-response retry, background responses, session creation/restoration, tool approvals, finalizers, usage accounting, and primary-failure preservation are tightly coupled operationally.
- Resource disposal is observable behavior. Double disposal or changed order can leak provider streams, MCP clients, HTTP clients, and context providers.
- Extraction must preserve tool invocation sequence numbers and failure sanitization used by process completion gates.
- A thin facade that still constructs every collaborator through `IServiceProvider` is not decomposition.

## Safe cutover sequence

1. Characterize each operational slice before extraction.
2. Move one responsibility at a time into a top-level collaborator and delegate from the facade.
3. Run focused streaming/session/finalizer/tool/disposal tests after every slice.
4. Switch DI to narrow adapters only after all collaborators are constructor-injected and no runtime service location remains.

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
