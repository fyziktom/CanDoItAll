# SB15-versioned-runtime-state-and-continuation: Versioned runtime state, per-proposal continuation, and context compatibility

## Metadata

- Phase: E — Continuation and runtime-state compatibility
- Depends on: `SB14-process-boundary-checkpoint`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Replace opaque unversioned session compatibility with an explicit runtime-state envelope and continuation policy while preserving original turn context/authority and enabling per-proposal approval decisions.

## Why this subbundle exists

Chat transcript, runtime session key, serialized MAF state, pending approval records, and in-memory approval cache currently cooperate through heuristics. Upgrades/provider changes/restarts need explicit compatibility decisions.

## Scope

- Implement `RuntimeStateEnvelope` persistence and MAF state adapter.
- Add compatibility/migration policy.
- Use stable per-approval decision commands.
- Tie continuation to original turn reference, authority fingerprint, provider/model/toolset/context-policy fingerprints.
- Bound/expire caches and define safe rehydration/failure.

## Non-goals

- Do not persist arbitrary opaque UI attachments as canonical state.
- Do not promise cross-version migration when it cannot be proven.

## Required SharedInfo skills

- `canonical-model-review`
- `csharp-testability-contracts`
- `csharp-modular-refactoring`
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

1. Persist new MAF state in `RuntimeStateEnvelope` with:
      - adapter ID;
      - envelope/schema version;
      - adapter/package version;
      - provider profile/transport/model;
      - history mode;
      - toolset and context-policy fingerprints;
      - captured timestamp;
      - opaque payload JSON.
2. Implement `IMafRuntimeStateAdapter` for serialize/deserialize and `IRuntimeStateCompatibilityPolicy` returning explicit outcomes: compatible restore, registered migration, safe canonical replay, or incompatible failure.
3. Add legacy reader for current `SerializedSessionStateJson` and runtime session key. Mark legacy envelope explicitly. Do not silently treat parse failure as compatible provider state.
4. Make pending approvals application-owned proposals with stable IDs and explicit mapping to MAF request/call IDs. Implement continuation command with a decision per proposal. Preserve decided, pending, and rejected records durably.
5. Keep original `AgentTurnContextReference`, authority ID/fingerprint, provider/model and toolset frozen for continuation. Validate them before replay. Never use current UI observation or a newly resolved broader authority.
6. Rename/refactor in-memory transient context registry to a bounded turn-context lease registry. Add TTL/eviction diagnostics that never evict an active waiting run silently. Terminal cleanup is mandatory.
7. Define restart behavior:
      - if the MAF envelope is compatible and all required turn attachments are canonically rehydratable, restore;
      - if MAF state is compatible but required attachment lease is not rehydratable, fail closed with a start-new-turn action;
      - if no attachments are required and safe replay is registered, replay canonical transcript/context reference;
      - never reconstruct from the current UI.
8. Add fingerprints to runtime state/continuation logs and telemetry, without raw payloads.
9. Migrate API/UI approval callers to per-proposal decisions. Keep bool compatibility only as a bounded facade with a deletion item in SB18 after SB17 stabilization.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Execution store owns pending proposals/decisions and envelope reference. MAF state adapter owns envelope payload serialization. Turn-context lease registry owns request-scoped attachments. Compatibility policy decides restore/migrate/replay/fail; current UI is never an input for old-run continuation.

## Dependency Direction

Runtime-state contracts are SDK-free. MAF payload serializer is in MAF. Application continuation coordinator depends on the state adapter/compatibility abstraction, not MAF concrete types.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Versioned envelope and explicit compatibility strategy. Use stable proposal IDs. Reject JSON-property heuristics as the sole compatibility mechanism.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Round-trip versioned MAF state envelope.
- Legacy state compatible migration and incompatible rejection.
- Provider/model/history/toolset/context-policy mismatch decisions.
- One approved, one rejected, one still pending proposal mapping.
- UI switches source before approval; original context and authority remain.
- Restart with rehydratable context succeeds; non-rehydratable required context fails closed.
- Cache/lease bounds, TTL, terminal cleanup and concurrent continuation.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- New runtime state always writes an envelope.
- No continuation reads current UI context registry.
- New production approval callers use stable-ID decisions.
- No raw opaque attachment serialization.

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

- `Approval/session/runtime state Unit and Integration tests.`
- `Existing approval round-trip and execution recovery tests.`
- `Release build and compatibility migration tests.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- Continuation compatibility is explicit and versioned.
- Original context/authority cannot be retargeted.
- Per-proposal decisions are supported.
- Legacy behavior is migrated or fails predictably.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- A migration widens authority or substitutes current UI state.
- State envelope omits provider/model/tool/context fingerprints needed for safety.
- Active waiting runs can lose leases silently.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- runtime state envelope/adapter/policy
- per-proposal continuation
- legacy migration
- lease lifecycle hardening
- tests/proof

## Downstream unlock

SB16 may start after continuation and compatibility tests pass.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Existing runs may wait for approval with legacy `RuntimeSessionKey`, unversioned JSON, pending approval records, and an in-memory transient context lease.
- Provider/model/toolset/context-policy mismatch must produce an explicit incompatibility result, not transcript replay or silent reset.
- Mapping a legacy boolean approval to all proposals is temporary compatibility behavior and must validate the exact pending-set fingerprint.
- Continuation must use the original turn context and authority even when the user now views another project or the live observation is unavailable.

## Safe cutover sequence

1. Add envelope writer, legacy reader, compatibility evaluator, and migration fixtures.
2. Persist new runs in the envelope while retaining legacy reads.
3. Migrate UI/API to per-proposal commands; keep the boolean adapter internal and instrumented.
4. Test restart and incompatible provider/model/toolset/context-policy cases before making the envelope required.
5. Remove legacy writes only; retain bounded legacy reads until the release retention decision.

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
