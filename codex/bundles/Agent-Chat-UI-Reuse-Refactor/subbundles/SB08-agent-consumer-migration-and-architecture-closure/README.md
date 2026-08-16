# SB08 — Agent consumer migration and architecture closure

## Status

`completed`

## Proof tier

`Governed`

## Dependency

- Depends on: SB07
- Closure checkpoint: CP4
- Owned requirements: UIR-004, UIR-012, UIR-014, UIR-016, UIR-017, UIR-018, UIR-019, UIR-024, UIR-025, UIR-031, UIR-033, UIR-044, UIR-045, UIR-046, UIR-054, UIR-061, UIR-064, UIR-073, UIR-075, UIR-077

## Objective

Migrate every existing agent consumer through the neutral presentation boundary, remove superseded duplication, prove dependency direction, and close architecture review without activating Simple Chats.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Migrate every live existing Agent consumer through the intended neutral presentation boundary and Agent facades.
- Remove superseded duplicate presentation code after proof.
- Close dependency direction, cycle, source guard, and architecture review.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`
- `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `every additional consumer discovered in SB01`

## Required deliverables

- `all live consumers using the approved neutral owner through Agent adapters/facades`
- `removed duplicate/superseded presentation paths`
- updated project and architecture documentation
- `before/after CodeAnalytics graph and hotspot evidence`
- completed C# architecture review
- cross-consumer affected tests
- `proof/SB08/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Re-run live references to ensure the SB01 consumer inventory is complete.
2. Migrate each consumer sequentially, preserving its current public contract and behavior.
3. Keep Process and contextual consumers from acquiring direct product-module dependencies.
4. Remove duplicate presentation helpers only after every consumer and owner test uses the intended owner.
5. Verify the old large types lost presentation responsibility and did not gain partial files or a replacement god mapper/service.
6. Refresh the scoped CodeAnalytics snapshot and compare dependency, cycle, findings, and large-type evidence.
7. Run actual-diff impacted-test analysis across all affected consumer workspaces.
8. Run source/phase guards and the C# architecture review gate.
9. Close CP4.

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

- Every required selector from the cross-consumer impact result.
- Affected project builds for neutral UI, AgentFramework.Components, Modules.AgentFramework, and Processes when changed.
- Focused component tests for each consumer family.
- A dependent-flow smoke only when CodeAnalytics identifies a critical cross-project path.
- No broad Stable gate here.

## Browser/UI proof

- Browser proof is supporting unless CP2/CP3 evidence was invalidated by migration.
- If invalidated, reopen the owning checkpoint and repeat the focused scenario rather than adding an unrelated broad pass.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [x] All live consumers are migrated or intentionally retain a documented compatibility facade.
- [x] No duplicate presentation owner remains.
- [x] Neutral project remains source-neutral.
- [x] No cycle or wrong dependency appears.
- [x] Old large types lost real responsibility.
- [x] No new partial expansion or service locator.
- [x] Cross-consumer impacted proof passes.
- [x] CP4 passes.

## Do not do

- Do not bypass the neutral project by copying markup into each consumer.
- Do not let Processes reference Modules.AgentFramework UI directly when a proper facade already exists.
- Do not remove compatibility APIs still used by a live consumer.
- Do not add Simple Chat UI while consolidating consumers.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `CP4` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
