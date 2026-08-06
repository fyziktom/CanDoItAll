# SB01-canonical-context-contracts: Canonical context, transition, authority, and runtime-state contracts

## Metadata

- Phase: A — Evidence and context foundation
- Depends on: `SB00-current-state-characterization`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Introduce SDK-free records and contracts that separate live UI observation, conversation affinity, immutable turn context, execution authority, and adapter runtime state. Do not change the production Send path yet.

## Why this subbundle exists

The existing `AgentRuntimeTransientContext` combines model content, workspace scope, and opaque attachments. A safe migration requires explicit concepts before behavior is rewired. These records become the vocabulary used by all later subbundles and prevent another overloaded compatibility object.

## Scope

- Add focused model files rather than expanding `ConversationModels.cs` or `FloatingAgentChatModels.cs` further.
- Define context epochs and transitions for same-project view changes and cross-source changes.
- Define safe durable turn-context and authority records plus runtime-only capture/lease shapes.
- Define a versioned, adapter-neutral runtime-state envelope.
- Add serializer, equality, invariant, and backward-compatible default tests.

## Non-goals

- No production context capture migration.
- No workspace service construction change.
- No MAF session persistence migration.
- No `PinnedToSource` conversation mode.

## Required SharedInfo skills

- `canonical-model-review`
- `csharp-modular-refactoring`
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

1. Create or confirm focused model files for:
      - `AgentUiObservationSnapshot` and contributor status/completeness;
      - `AgentConversationContextBinding`, mode, revision, and `AgentContextEpochId`;
      - `AgentContextTransition` kind/decision;
      - `AgentTurnContextReference`, safe durable context metadata, and runtime-only capture descriptor;
      - `AgentExecutionAuthorityRecord` and policy fingerprint;
      - `RuntimeStateEnvelope` and adapter/schema identifiers.
2. Define invariants:
      - IDs cannot be empty;
      - versions/revisions are monotonic positive values where required;
      - source kind/id and workspace scope are normalized;
      - a mutation authority implies read authority;
      - a runtime-state envelope must identify adapter and schema;
      - durable records cannot contain opaque attachment objects or secrets.
3. Define `FollowCurrentSurface` and `Detached` only. Document why `PinnedToSource` is deferred until canonical source rehydration exists.
4. Define transition classification data without implementing classification logic. Include previous/current binding references, transition kind, epoch behavior, and safe model-facing summary metadata.
5. Separate safe durable authority identity from the richer runtime authorization object. The durable record stores IDs, scope, allowed-operation identifiers, generation, and fingerprints; it does not serialize service handles or secret grants.
6. Add version fields and backward-compatible defaults. Ensure old serialized chat/run documents can be read when the new optional properties are absent.
7. Add direct tests for constructor validation, JSON round trips, equality, normalization, invalid enums, oversized bounded text, and forbidden null/empty combinations.
8. Add a source assertion that the new model files contain no MAF/MEAI/OpenAI/UI/module namespace.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Models owns immutable SDK-free records. Core/application will later own capture and transition behavior. Product modules provide observations and canonical authority adapters. MAF owns only adapter payload mapping.

## Dependency Direction

The Models project may reference only its existing low-level abstractions. New context/authority/runtime-state records must not reference Core, MAF, UI, persistence implementations, product modules, or AI SDKs.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Use explicit value objects and discriminated enums/records. Prefer composition over adding flags to `AgentRuntimeTransientContext`. Reject a single `AgentContextV2` bag that reproduces the current ambiguity.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Pure unit tests only; no filesystem, database, provider, or full host.
- Negative tests for empty identities, mutation-without-read, invalid envelope schema, and attachment leakage into durable records.
- Backward compatibility test that old JSON without V2 fields deserializes to safe defaults.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- New contracts are in focused files.
- No SDK or product-module namespace in model contracts.
- No behavior implementation in Models.

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

- `Build Models and Core projects.`
- `Run focused model contract tests.`
- `Run serialization compatibility tests.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Each state category in ADR-001 has a distinct named record and owner.
- No new universal context bag exists.
- All records are SDK-free and independently tested.
- Existing production code still compiles and behaves unchanged.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- A new contract would create a Core/Models/project cycle. Extract a smaller value object rather than adding the reference.
- A durable record requires serializing an opaque UI attachment. Redesign the reference/lease split.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- new model contracts
- contract tests
- updated canonical model map
- proof manifest

## Downstream unlock

SB02 may start after contract tests pass and canonical-model review finds no overloaded new record.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- A single universal context DTO would recreate the current source-of-truth ambiguity under a new name.
- Context, authority, runtime state, and conversation affinity have different persistence and trust semantics; they must not share one mutable property bag.
- New model projects can create cycles if UI-facing types or Core behavior are pulled into Contracts.
- V1 metadata and runtime-state fixtures must remain readable without reinterpreting old authority under V2 rules.

## Safe cutover sequence

1. Add SDK-free contracts and V1 adapters without changing the production writer.
2. Prove serialization/round-trip and dependency direction.
3. Add V2 readers and explicit compatibility outcomes.
4. Enable V2 construction only behind the capture service added in SB02; do not delete V1 readers.

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
