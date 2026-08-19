# SB04 — Conversation thread rail and history extraction

## Status

`pending`

## Proof tier

`Behavioral`

## Dependency

- Depends on: SB03
- Closure checkpoint: none
- Owned requirements: UIR-030, UIR-031, UIR-032, UIR-033, UIR-073, UIR-075, UIR-077

## Objective

Extract the reusable thread list, search, empty/loading states, item presentation, and bounded history dialog while leaving all agent session orchestration and approval semantics in the agent adapter.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Extract neutral conversation thread rail, thread item, search/count/empty/loading/error presentation, and bounded history dialog.
- Adapt AgentChatPanel thread/session rendering and AgentThreadHistoryDialog without changing workspace/session services.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThreadHistoryDialog.razor`
- `tests/Components/CanDoItAll.Tests.Components/AgentChatPanelResponsivenessTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentChatModalTests.cs`

## Required deliverables

- neutral thread presentation record
- `neutral thread rail/list item/history dialog`
- Agent session-to-thread mapper
- AgentChatPanel presentation integration with unchanged backend orchestration
- direct neutral thread tests
- `Agent thread/history regression tests`
- `proof/SB04/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Inventory current session summary fields, ordering, filtering, selected state, approval badges, auto-approval state, title editing, and history behavior.
2. Create neutral thread records and focused components.
3. Supply pending-approval/auto-approval presentation through adapter metadata or adornment slots.
4. Keep session load/create/select/rename/history commands in AgentChatPanel or focused Agent application adapters.
5. Do not move service injection into the neutral project.
6. Preserve stable keys and selected item behavior when projections are recreated.
7. Run actual-diff impacted-test analysis, owner tests, and source guards.

## Architecture and dependency gate

- Use the narrowest healthy CodeAnalytics snapshot for architecture-relevant changes.
- Keep source-neutral presentation free of AgentFramework, LlmChats, backend, persistence, and runtime dependencies.
- Reject cycles, wrong project-reference direction, service location, partial-class growth, facade-only extraction, and boolean-god components.
- Record what the old owner no longer owns after this subbundle.
- Run `csharp-architecture-review-gate` when this subbundle closes a named architecture checkpoint.

## Impacted-test protocol

For every production change in this subbundle:

1. derive actual diff files and one-based changed line ranges;
2. call `code_analytics_impacted_tests_get` with `behaviorIntent=Unknown`;
3. put inspected-only files in `contextOnlyPaths`;
4. verify healthy workspaces, resolved symbols, and nonzero source/test discovery;
5. run every required selector;
6. promote conditional selectors only when a returned trigger occurs;
7. use `BehaviorPreservingImplementation` only after conservative analysis justifies it;
8. record request, response, selectors, discovery counts, commands, results, containment, and promotion decisions.

## Focused test intent

- Neutral search, empty/loading/error, selected state, metadata/badges, new/refresh callbacks, and history selection.
- Agent session mapping and pending approval badges.
- Current new/select/rename/search/history behavior.
- Nonzero discovery for every required selector.

## Browser/UI proof

- A browser pass is optional unless the rail/dialog DOM, CSS isolation, scroll, or focus changes materially.
- When required, compare thread rail and history dialog at the SB01 viewport.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] Thread presentation is neutral and directly testable.
- [ ] Session services and commands remain Agent-owned.
- [ ] Search/order/selection/new/rename/history behavior matches baseline.
- [ ] Approval state remains adapter-provided.
- [ ] No unbounded load is introduced.
- [ ] Impacted proof passes.

## Do not do

- Do not change transcript or composer yet except minimal integration needed to keep compilation.
- Do not refactor Agent workspace persistence.
- Do not add Simple Chat conversation APIs.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `none` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
