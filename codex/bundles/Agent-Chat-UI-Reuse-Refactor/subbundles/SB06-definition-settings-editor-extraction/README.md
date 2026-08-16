# SB06 — Definition settings and editor-surface extraction

## Status

`pending`

## Proof tier

`Behavioral`

## Dependency

- Depends on: SB05
- Closure checkpoint: none
- Owned requirements: UIR-050, UIR-051, UIR-052, UIR-053, UIR-054, UIR-073, UIR-075, UIR-077

## Objective

Extract reusable editor shell, identity/avatar/instructions fields, provider/model selection presentation, and optional advanced-setting slots without moving agent-only runtime policy or binding to Simple Chat domain types.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Extract reusable definition editor shell, identity/avatar/name/summary/instructions fields, provider/model presentation, optional temperature field, and advanced-settings slot.
- Integrate AgentDetailsDialog and ProviderModelSelector through Agent adapters while retaining all Agent-only tabs and save semantics.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThinkingEffortSettings.razor`
- `tests/Components/CanDoItAll.Tests.Components/AgentDetailsDialogAvatarGenerationTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentDetailsDialogCapabilityTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentDetailsDialogDeletionTests.cs`

## Required deliverables

- neutral editor shell and identity fields
- `neutral provider/model option records and selector presentation`
- optional neutral temperature field and advanced-settings slot
- `Agent ProviderProfile/model mapping facade`
- `AgentDetailsDialog markup integration with unchanged service/save/delete/version ownership`
- direct neutral settings tests
- Agent settings regression tests
- `proof/SB06/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Inventory all AgentDetailsDialog tabs, editor state, validation, save/delete/version behavior, avatar actions, provider/model defaults and override behavior.
2. Use Components MCP recommendations for compact form and dialog composition.
3. Extract only source-neutral identity/editor field groups.
4. Use configurable labels so current Agent UI says Instructions while a future adapter can say System prompt.
5. Represent provider choices with neutral option records and keep ProviderProfile mapping in AgentFramework.
6. Keep Agent reasoning effort in an adapter or advanced-settings slot unless represented without importing Agent enums.
7. Retain Agent-specific tabs and policies in AgentDetailsDialog.
8. Preserve code-behind service calls and domain validation.
9. Run actual-diff impacted-test analysis, owner tests, and source guards.

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

- Neutral identity field binding, labels, disabled state, validation messages, and avatar action callbacks.
- Neutral provider default/model list/custom override behavior.
- Agent ProviderProfile mapping.
- Agent avatar choose/default/generate behavior.
- Agent save/delete/version and all affected Agent-only tab behavior.
- Expected nonzero discovery for every required selector.

## Browser/UI proof

- Run a focused settings dialog proof when markup/CSS/dialog structure changes materially.
- Inspect identity and runtime sections plus one Agent-only tab to prove the extraction did not hide or reorder product behavior.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] Reusable fields are neutral and directly testable.
- [ ] No ProviderProfile or LlmChatDefinition leaks into neutral contracts.
- [ ] Agent save/load/delete/version behavior remains Agent-owned and unchanged.
- [ ] Agent-only tabs remain present and functional.
- [ ] Current labels/test ids/accessibility and compact layout are preserved.
- [ ] Impacted proof passes.

## Do not do

- Do not bind the neutral editor to LlmChatDefinition.
- Do not move Agent persistence or validation into the neutral project.
- Do not remove or genericize Agent-only policy tabs.
- Do not add Simple Chat settings pages.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `none` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
