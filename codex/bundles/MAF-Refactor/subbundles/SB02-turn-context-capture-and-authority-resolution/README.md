# SB02-turn-context-capture-and-authority-resolution: Turn-context capture and canonical authority resolution

## Metadata

- Phase: A — Evidence and context foundation
- Depends on: `SB01-canonical-context-contracts`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Replace the monolithic invocation mapping with an application pipeline that captures the current UI observation, resolves canonical execution authority, composes model context, and binds an immutable turn reference to the execution run.

## Why this subbundle exists

Floating context is useful only if the next turn sees the current surface. It is safe only if the surface cannot grant authority. This subbundle creates the join point between the UI-observation timeline and the execution timeline.

## Scope

- Introduce narrow capture, authority, transition-input, and model-context services.
- Migrate the floating Send path to V2 turn capture behind a short-lived compatibility adapter.
- Derive runtime workspace scope from canonical authority, never directly from UI transient context.
- Persist safe turn reference and authority identity/fingerprint with the run.
- Preserve exact request-scoped opaque attachments through the existing continuation lease invariant.

## Non-goals

- No per-conversation affinity/transition behavior yet; use a neutral initial binding.
- No Gantt rich facts yet.
- No workspace service factory or MAF runtime split yet.

## Required SharedInfo skills

- `csharp-modular-refactoring`
- `csharp-testability-contracts`
- `canonical-model-review`
- `csharp-factory-builder-composition`

Read `../../sharedinfo/required-skills.md` and the corresponding installed skills before editing.

## Pre-flight

1. Verify dependencies are closed with an `Unlocked` decision.
2. Rebase/merge the current `development` branch and record HEAD.
3. Read the root architecture, ADR, plan, and evidence files relevant to this scope.
4. Create/refresh a CodeAnalytics snapshot when available.
5. Copy `../../templates/subbundle-proof-manifest.json` to `proof/proof-manifest.json` and fill it during work.
6. Add failing-first or characterization proof before moving behavior.

## Detailed implementation tasks

1. Define narrow interfaces such as:
      - `IAgentTurnContextCaptureService`;
      - `IAgentExecutionAuthorityResolver`;
      - `IAgentModelContextComposer`;
      - `IAgentTurnContextLeaseRegistry` or a V2 replacement for the current transient registry.
      Interface names may change, but responsibilities may not be merged.
2. Implement capture order:
      1. capture strict live observation and navigation identity;
      2. validate database profile generation;
      3. determine requested source identity;
      4. resolve canonical read/mutation authority;
      5. compare observation source/scope with authority;
      6. compose bounded model context;
      7. create turn reference/digest and request-scoped lease;
      8. admit the execution with safe metadata.
3. Do not treat `AgentChatContextAgentAccess` as final authority. It may short-circuit an obviously unavailable context, but the canonical resolver must independently prove the agent/principal/product permissions.
4. Move workspace scope selection out of `AgentRuntimeTransientContext`. During migration, construct the old record from the V2 capture only after authority resolution and set its scope from the authority snapshot.
5. Persist or map safe fields on `ExecutionRunRecord`/detail:
      - turn capture/reference ID;
      - observation source/version;
      - context epoch placeholder;
      - transition placeholder;
      - model-context digest;
      - authority ID, scope, generation, policy fingerprint.
      Prefer typed optional properties over expanding untyped metadata when persistence mapping permits.
6. Bind the runtime lease to the execution run ID and verify its digest against the safe reference. Retain current fail-closed behavior when the required lease is unavailable.
7. Make completion notification originate from the admitted turn reference, not from whatever context is current when completion occurs.
8. Migrate `AgentChatExecutionOrchestrator` to depend on the capture service instead of implementing capture/generation/invocation details itself.
9. Add telemetry with IDs/fingerprints only. Do not log model context, raw attachments, or secrets.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

`AgentChatContextRegistry` remains the live observation owner. A new Core/application turn-capture service owns turn assembly. An outer canonical authority resolver owns execution authority. The execution store owns the safe admitted reference. MAF receives the result but does not resolve authority.

## Dependency Direction

Core depends on Models and authority/capture abstractions. Product authorization implementations depend inward on those abstractions. MAF must not be referenced by the turn-capture pipeline.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Application service pipeline plus policy resolver. Split responsibilities currently in `AgentChatContextInvocationFactory` into capture, authority resolution, model composition, and metadata mapping. Keep a temporary adapter only for existing callers.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Capture succeeds for an authorized Project X Canvas observation.
- UI says mutate but canonical authority is read-only; mutation authority is false and mutation tools will later be filtered.
- UI source/scope mismatch with canonical authority fails before runtime call.
- Database profile generation changes during capture; admission fails/retries according to explicit policy.
- Exact opaque attachment fingerprints and digest bind to the admitted run.
- Completion notification uses original source after navigation.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- `AgentChatExecutionOrchestrator` no longer constructs transient context/metadata directly.
- Production Send path calls V2 capture service.
- No runtime scope is read directly from UI observation after authority resolution.

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

- `Focused Unit and Components tests.`
- `Build Models, Core, AgentFramework module, Workbench.`
- `Existing floating context and activity admission tests.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- One admitted turn has one immutable context reference and one authority fingerprint.
- UI observation cannot elevate authority.
- Runtime scope comes from canonical authority.
- Original context lease and completion source invariants remain intact.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- No canonical authority service exists for a source. Add a source-specific resolver/adapter; do not trust UI publication.
- Persistence requires storing opaque attachment objects. Keep them in the lease and persist fingerprints only.
- The migration creates dual independently executing Send paths. Make one path authoritative.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- turn capture pipeline
- authority resolver seam/implementation
- V2 metadata/reference persistence
- direct tests
- proof manifest

## Downstream unlock

SB03 may start when the V2 Send path is authoritative and UI-authority negative tests pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- The current UI publication carries a workspace scope near the execution path. Treat it as a claim to validate, never as authority.
- Database-profile generation can change between UI capture, operation admission, authority resolution, and execution creation.
- Committing conversation affinity before authority/admission succeeds can leave a thread bound to a context that never executed.
- A failed or stale attachment must fail before any capability or workspace service is created.

## Safe cutover sequence

1. Build the new capture and authority services beside the current invocation mapper.
2. Shadow-compare only pure observation/transition/digest mapping; never resolve or execute tools twice.
3. Switch one send-entry path atomically to `Capture -> ResolveAuthority -> Admit -> Persist reference -> Execute`.
4. Keep the legacy path available for rollback until SB05, but expose telemetry proving which path was selected.

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
