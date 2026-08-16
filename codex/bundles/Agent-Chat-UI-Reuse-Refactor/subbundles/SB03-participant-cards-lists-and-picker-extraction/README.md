# SB03 — Participant cards, compact lists, and picker extraction

## Status

`pending`

## Proof tier

`Behavioral`

## Dependency

- Depends on: SB02
- Closure checkpoint: none
- Owned requirements: UIR-020, UIR-021, UIR-022, UIR-023, UIR-024, UIR-025, UIR-026, UIR-073, UIR-075, UIR-077

## Objective

Extract reusable participant presentation surfaces while keeping agent ordering, favorites, team semantics, actions, copy, and test selectors behaviorally unchanged through compatibility adapters.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Extract neutral participant card, compact list/item, and picker presentation.
- Adapt current AgentSelectionCard, AgentCompactList, AgentCompactListItem, AgentSwitchDialog, and relevant AgentCatalogPanel composition.
- Preserve current agent semantics through adapters and slots.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactList.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactListItem.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSwitchDialog.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor`
- `tests/Components/CanDoItAll.Tests.Components/AgentCatalogPanelTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentCompactListTests.cs`

## Required deliverables

- neutral participant presentation records
- `neutral participant card/list/item/picker components`
- `focused Agent participant mapper/adapter types`
- compatibility facades or preserved public Agent component APIs
- direct neutral owner tests
- Agent mapping and behavior regression tests
- `proof/SB03/ui-parity.md`
- `proof/SB03/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Inspect current public parameters, CSS, test ids, accessible names, ordering, filtering, favorites, team selection, and action propagation.
2. Use Components MCP recommendations and real examples before changing card/list/picker composition.
3. Define the smallest participant presentation record and generic badges/meta/actions.
4. Use opaque string keys and stable Razor keys.
5. Implement neutral card/list/item/picker presentation.
6. Map AgentDefinition and agent-only semantics in focused Agent adapters.
7. Keep team tree, capabilities, workload, provider privacy, managed-agent actions, and favorite persistence outside the neutral project.
8. Preserve existing public facades while migrating internals.
9. Run actual-diff impacted-test analysis, direct owner tests, and source guards.
10. Inspect focused rendered parity when CSS or DOM ownership changes.

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

- Neutral rendering for identity, avatar fallback, badges, tags, metadata, selected/busy/disabled states, actions, and non-Guid keys.
- Agent facade mapping for status/workload/private/history/capabilities/favorites.
- AgentCatalogPanel search/order/team/favorite/managed-agent behavior.
- AgentSwitchDialog search/tag/favorite/select/double-click/close behavior.
- Every required selector from impact analysis with expected nonzero discovery.

## Browser/UI proof

- Use a focused catalog/switcher proof only when CSS isolation or markup composition changes materially.
- Compare normal card grid, compact list, selected/busy state, and open switch dialog against SB01.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] Neutral components have no Agent type.
- [ ] Agent public facades remain callable.
- [ ] AgentCatalogPanel and switcher behavior is unchanged.
- [ ] Agent-only semantics are supplied by adapters or slots.
- [ ] Opaque keys work.
- [ ] Owner and impacted tests pass.
- [ ] No Simple Chat production reference appears.

## Do not do

- Do not add Simple Chat cards or filters.
- Do not generalize Agent team/capability policy into the neutral contract.
- Do not move Agent services into the neutral project.
- Do not remove compatibility facades before all consumers are proven.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `none` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
