# SB03-floating-conversation-affinity-and-transitions: Floating conversation affinity, context epochs, and transitions

## Metadata

- Phase: A — Evidence and context foundation
- Depends on: `SB02-turn-context-capture-and-authority-resolution`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Give each floating chat thread an explicit context binding so that the same conversation follows Project Structure Canvas -> Gantt, reports the transition on the next turn, and starts a new context epoch when the source entity or module changes.

## Why this subbundle exists

The live registry knows only the current UI state. It cannot answer what a particular chat was following on its previous turn. Without affinity and epochs, the agent receives fresh context but cannot reliably distinguish a view change from stale transcript facts.

## Scope

- Add a chat/session-owned conversation context service and persistence seam.
- Classify transitions between the prior binding and current observation.
- Integrate binding/transition into turn capture and model context.
- Expose current/pending context in floating chat UI.
- Preserve one transcript while marking strong context changes with a new epoch.

## Non-goals

- No automatic model invocation on navigation.
- No pinned inactive-source mode.
- No automatic transcript deletion or new chat on project switch.

## Required SharedInfo skills

- `canonical-model-review`
- `csharp-modular-refactoring`
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

1. Implement `IAgentConversationContextService` and store:
      - create/get binding by floating handle before session creation;
      - bind/transfer to `ChatSessionId` after first Send;
      - update mode and adopted source;
      - compare-and-swap or expected revision on update;
      - remove/expire pending handle bindings with chat cleanup.
2. Implement `AgentContextTransitionClassifier` with deterministic rules from ADR-002. Classification uses typed source kind/id, workspace position, surface/view and selection identity; it does not inspect free-form model text.
3. Implement context epochs:
      - same source entity + view/selection change keeps epoch;
      - source entity or source kind change creates a new epoch;
      - detached/unavailable transitions create a new epoch;
      - epoch identity is included in the turn reference and trusted model header.
4. Default floating chats to `FollowCurrentSurface`. Add an explicit `Detached` action in the chat UI. Do not silently detach on capture failure; show an actionable state.
5. Add a trusted model-context section:
      - current source/display name/surface/view;
      - transition from the prior binding;
      - context epoch notice;
      - statement that prior epoch UI facts are historical;
      - no raw authorization details or secrets.
6. Update `ActiveAgentChat` projection/UI with safe fields such as following display name, surface/view, mode, context epoch, and a pending-transition indicator. Keep the model-independent navigation update cheap.
7. When the user merely changes tabs, update observation and UI badge only. Confirm no call to execution coordinator/provider.
8. When Send succeeds/admission is accepted, update the binding revision to the captured source/epoch. Define failure behavior: a failed admission must not falsely claim that the thread adopted an uncaptured source.
9. Handle concurrent navigation and Send by relying on strict observation capture and expected binding revision. Retry only the safe capture/classification stage; never duplicate provider execution.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

The conversation context service owns binding and epoch state. The live observation registry remains global/current per UI scope. The turn capture service combines them at Send. The transcript store remains conversation history, not current UI truth.

## Dependency Direction

Conversation binding contracts live in Models/runtime-neutral projects. Persistence interfaces live inward; file/database implementations live outward. UI components consume the service through narrow commands/queries.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

State machine with immutable revisions. Use explicit transition classification rather than string comparison embedded in the Razor host. Use a pending handle binding before a chat session exists, then transfer atomically to the session identity.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Canvas -> Gantt: `ViewChanged`, same epoch, same chat session.
- Selection A -> B: `SelectionChanged`, same epoch.
- Project X -> Y: `SourceEntityChanged`, new epoch, same transcript/session.
- Project Structure -> another module: `SourceKindChanged`, new epoch.
- Detached mode: no application observation/authority in the next turn.
- Navigation only: zero execution/provider calls.
- Concurrent navigation at Send: either a consistent old or new snapshot is captured, never a mixed snapshot.
- Binding transfer from floating handle to created chat session is atomic/idempotent.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No transition logic in Razor markup/code-behind beyond invoking the service.
- No synthetic user transcript message on navigation.
- No `PinnedToSource` implementation without a rehydrator.

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

- `Unit tests for state machine/classifier/store.`
- `Component tests for badges, detach, navigation-without-provider.`
- `Integration test across chat session creation and context binding transfer.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- The next turn after Canvas -> Gantt explicitly knows the view changed.
- A running/approval turn is unaffected by later binding changes.
- Cross-project transitions re-resolve authority and identify prior UI facts as historical.
- The user can see what context the chat will use.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- Binding persistence would store full UI fragments or opaque attachments. Persist safe metadata only.
- A project switch reuses the old authority snapshot. It must resolve a new one.
- Navigation triggers provider execution. Remove the side effect.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- conversation context service/store
- transition classifier
- epoch model integration
- floating UI state
- tests/proof

## Downstream unlock

SB04 may start after practical transition scenarios pass and no provider call occurs on navigation.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Multiple floating chat windows share one live UI observation registry but require independent per-session affinity.
- Component disposal, hidden chats, reopened history, and rapid navigation can produce stale event handlers or cross-chat contamination.
- Project X -> Project Y is an epoch change, not merely another view transition.
- A running turn must not be rewritten when the live UI moves; only the next explicit turn follows the new surface.

## Safe cutover sequence

1. Introduce affinity storage with no automatic mutation of existing sessions.
2. Observe/classify transitions in shadow mode and compare expected scenarios.
3. Commit affinity only after successful operation admission and authority resolution.
4. Enable FollowCurrentSurface for new floating chats; preserve existing thread behavior through a compatibility default.

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
