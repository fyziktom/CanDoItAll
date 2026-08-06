# SB04-project-structure-gantt-observation: Project Structure and Gantt observation contributors

## Metadata

- Phase: A — Evidence and context foundation
- Depends on: `SB03-floating-conversation-affinity-and-transitions`
- Checkpoint: No
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Baseline repository: `fyziktom/CanDoItAll`, branch `development`

## Goal

Provide bounded, view-specific Project Structure/Gantt facts to the next floating-agent turn while keeping canonical project data and mutation authority in the owning Workbench services/tools.

## Why this subbundle exists

The Project Structure parent already tells the agent whether the active view is Canvas or Gantt, but the Gantt panel owns richer projection facts such as task/dependency counts, schedule range, warnings, ordering and selection. The current context builder also mixes volatile UI facts with long-lived tool guidance.

## Scope

- Split Project Structure observation publication by responsibility.
- Add a Gantt observation contributor close to the panel/projection owner.
- Move stable tool/protocol guidance out of volatile UI fragments.
- Publish bounded projection summaries, freshness and completeness state.
- Preserve canonical mutation/query paths and existing context attachments.

## Non-goals

- Do not make the Gantt projection a second canonical task store.
- Do not dump every task or full graph into every prompt.
- Do not let Gantt UI state grant mutation authority.

## Required SharedInfo skills

- `csharp-modular-refactoring`
- `csharp-provider-tool-plugin-isolation`
- `canonical-model-review`
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

1. Refactor `ProjectStructureAgentChatContextBuilder` into focused builders/contributors. Preserve output compatibility first, then remove duplicate old composition.
2. Add `ProjectStructureGanttObservationContributor` supplied by `ProjectStructureGanttPanel` or a narrow panel-owned publisher. Publish only when Gantt is active and the navigation/scope identity matches.
3. Define bounded Gantt facts, for example:
      - project ID/name and active view;
      - projection revision/fingerprint;
      - task, dependency, milestone/unscheduled counts when available;
      - projected date range;
      - warning/error counts and bounded top issue codes/summaries;
      - selected task/row or visible range when the component exposes it;
      - row-order/view-state fingerprint;
      - loading/partial/failed completeness.
      Do not include entire task descriptions or hidden sensitive fields by default.
4. Add a typed opaque `ProjectStructureGanttObservationAttachment` only for exact view-state facts that need module-owned interpretation. It must include content/coverage/freshness fingerprints and must not itself authorize product access.
5. When the Gantt projection is loading, publish explicit `Partial/Loading` observation facts instead of retaining stale Canvas facts. Allow the user to send when safe, with a clear statement that exact visible projection is unavailable; canonical tools may still query authorized product data.
6. Move durable operational instructions such as tool protocol, exact mutation/readback expectations, and authority guidance from the UI fragment into `ProjectStructureRuntimeGuidanceContributor` or an equivalent Workbench-owned registered runtime contributor. Keep UI fragments factual and time-varying.
7. Retain product mutation/query through existing Workbench application services and runtime tool providers. A visible projection summary is model context only; exact changes require typed tools and canonical readback.
8. Wire contributor completion refresh carefully. An old Canvas or Gantt run may request refresh of its originating source, but it must not overwrite an unrelated current context.
9. Measure context size before/after. Set explicit limits and deterministic truncation/summarization for issues and selections.

## C# Architecture Impact

This is an architecture-relevant subbundle. Update the responsibility inventory, boundary map, dependency graph, pattern records, and testability plan when the implementation differs from the planned shape. A passing build alone is not closure proof.

## Boundary Ownership

Workbench owns Project Structure/Gantt observation contributors and stable runtime guidance. Project Structure canonical services own task/node data and mutations. Core owns only generic observation aggregation/capture contracts.

## Dependency Direction

Workbench may reference AgentFramework context contracts. Core/Models must not reference Workbench or Gantt component packages. Runtime guidance enters through a registered Workbench-owned context contributor, not a MAF hard-coded branch.

Any `.csproj` change requires before/after project-reference evidence and a cycle check. Do not solve cycles through broad Common/Shared projects, reflection, `object`, or service location.

## Pattern Decision

Contributor composition. Separate base project facts, active-view facts, selection facts, Gantt visible projection facts, and stable runtime guidance. The aggregator validates duplicate contributor IDs and deterministic order.

Record any material deviation as an ADR amendment with rejected alternatives and proof requirements.

## Testability Contract

Required tests/proof:

- Gantt active with ready projection publishes Gantt contributor and bounded facts.
- Canvas active excludes Gantt contributor.
- Gantt loading publishes explicit partial/loading state, not stale Canvas facts.
- Projection warnings are bounded and deterministic.
- View selection/timeline facts update the next observation version.
- Observation cannot mutate product state or alter authority.
- Stable runtime guidance appears once through its registered owner and not in the volatile UI fragment.
- Context size remains under configured limits.

Tests for extracted behavior must instantiate the extracted owner directly. At least one negative test must fail if the implementation merely delegates back to the old monolith or trusts the wrong authority.

## Partial Class Policy

- Do not add a new partial class as the final architecture boundary.
- Do not move behavior into a nested class under the old runtime/service.
- A temporary partial is allowed only for mechanical compilation sequencing, must be named in the proof manifest, and must be deleted before this subbundle closes unless the checkpoint explicitly blocks closure.

## Architecture Proof Required

Source assertions:

- No Core/Models reference to Workbench or Gantt package.
- No duplicate Project Structure tool guidance in UI and runtime contributor.
- Gantt contributor has a unique descriptor/contributor ID.

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

- `Workbench project build.`
- `Focused Unit and Components tests for Project Structure/Gantt context.`
- `Existing Gantt mutation/projection tests.`
- `Floating Canvas/Gantt end-to-end context test.`

Use narrower filters during development, then run the complete required set before closure.

## Acceptance criteria

- After switching to Gantt, the next turn knows Gantt and receives useful bounded Gantt facts.
- Canonical task/project truth remains owned by Project Structure services.
- Loading/failed projection states are explicit.
- Volatile UI facts and stable runtime guidance have separate owners.

## Stop and repair conditions

Stop this subbundle and repair the plan when:

- The contributor needs to copy the whole canonical project graph into chat state. Use bounded summary plus canonical tools.
- The Gantt component cannot publish without creating a Core -> Workbench dependency. Invert through the existing context publication contract.
- Context exceeds bounds or leaks hidden data. Redesign the summary.

Do not hide a blocker in a follow-up issue when it affects authority, source of truth, dependency direction, scope identity, persistence compatibility, or testability.

## Required deliverables

- split observation contributors
- Gantt observation model/publisher
- runtime guidance contributor
- component/unit tests
- context-size proof

## Downstream unlock

SB05 checkpoint may start after the practical Gantt next-turn test passes.

## Claude Code execution profile

- Primary executor: Claude Code.
- Preferred model: Claude Fable 5.
- Reasoning profile: use the deepest/maximal reasoning mode available in the installed Claude Code version. The phrase `xHigh` expresses intent only; do not invent or require a non-existent CLI flag.
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model configured in the environment. Preserve this subbundle's proof, stop conditions, and architecture gates when switching models.
- Work on exactly this subbundle. Do not opportunistically implement a later subbundle because related files are open.
- Use installed SharedInfo skills and CodeAnalytics MCP as evidence sources. Treat MCP summaries as orientation, then inspect exact source and project files before editing.
- Persist decisions, commands, failures, and remaining work in the subbundle proof directory so another Claude session or model can resume without conversational memory.

## High-risk adaptation points

- Gantt is a projection over canonical Project Structure data; publishing projection facts must not create a second writable task model.
- Projection rebuilds, assignment loads, row-order updates, and timeline interactions can trigger high-frequency context updates and render loops.
- Large task lists must remain bounded; exact plan facts continue to come from canonical tools.
- Current parent-level view labels are useful fallback behavior and should remain until the Gantt contributor is proven.

## Safe cutover sequence

1. Add the contributor behind a bounded publication path; keep existing coarse view fragment as fallback.
2. Publish only when a stable projection fingerprint changes.
3. Validate size, render count, and no canonical mutation.
4. Remove redundant parent prose only after the contributor passes component and context-capture tests.

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
