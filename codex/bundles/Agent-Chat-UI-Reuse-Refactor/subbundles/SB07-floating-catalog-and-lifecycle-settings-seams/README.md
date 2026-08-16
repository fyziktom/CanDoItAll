# SB07 — Floating catalog and lifecycle-settings seams

## Status

`pending`

## Proof tier

`Behavioral`

## Dependency

- Depends on: SB06
- Closure checkpoint: CP3
- Owned requirements: UIR-055, UIR-060, UIR-061, UIR-062, UIR-063, UIR-064, UIR-073, UIR-075, UIR-077, UIR-078

## Objective

Extract floating-window presentation, catalog/list composition, and generic active-chat lifecycle fields while keeping the current host agent-only and preserving context, handle, retention, and preparation behavior.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Extract floating catalog/window/list presentation seams using the neutral participant components.
- Extract generic active-chat lifecycle retention/capacity field presentation.
- Keep FloatingAgentChatHost and settings behavior Agent-only.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatSettingsPanel.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactList.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`

## Required deliverables

- `neutral floating catalog/window/list presentation component(s)`
- neutral active-chat lifecycle settings fields
- Agent host projections and adapters
- Agent-only prepared activation stock retained in its owner
- `floating host/settings owner tests`
- `CP3 normal/open-overlay browser evidence`
- `proof/SB07/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Inventory host tabs, filters, active handles, visible/hidden state, close/hide/stop behavior, context access, affinity, history, preparation, retention, and settings.
2. Use existing BaseLib overlay/window components and Components MCP guidance instead of adding custom structural wrappers.
3. Replace direct Agent list markup with neutral participant projections while keeping all coordinator actions Agent-owned.
4. Extract generic retention/capacity fields; keep prepared-Agent metadata stock and adaptive preparation in Agent settings.
5. Do not change production labels to mixed-source language.
6. Do not add Simple Chat filters, entries, routes, or context actions.
7. Run actual-diff impacted-test analysis, owner tests, source guards, and CP3 focused browser proof.
8. Close CP3.

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

- Neutral floating catalog list and callbacks.
- Generic lifecycle field binding/validation/status.
- Agent coordinator mapping and handle identity.
- search/new/history/open/active/hidden behavior.
- close versus stop and retention/capacity behavior.
- context access and affinity behavior.
- prepared-Agent settings remain unchanged.
- Every required selector with expected nonzero discovery.

## Browser/UI proof

- Mandatory CP3 desktop overlay proof.
- Inspect closed/open catalog, search, active list, visible chat, hidden/reopened chat, history/switch overlay, context/affinity controls, and settings.
- Check z-index, focus, clipping, internal scroll, visible actions, and close/hide/stop semantics.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] Floating presentation seam is neutral.
- [ ] Host services/coordinator/context/handles remain Agent-owned.
- [ ] No mixed catalog/filter appears.
- [ ] No Add context feature appears.
- [ ] Retention/capacity and prepared-Agent settings behave as before.
- [ ] Overlay parity passes.
- [ ] CP3 passes.

## Do not do

- Do not rename current product labels to imply Simple Chats.
- Do not create a multi-source coordinator.
- Do not move context or handle lifecycle into the neutral project.
- Do not add project-structure context capture.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `CP3` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
