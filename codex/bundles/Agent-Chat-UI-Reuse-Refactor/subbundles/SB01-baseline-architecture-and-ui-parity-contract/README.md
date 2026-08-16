# SB01 — Baseline, architecture inventory, and UI parity contract

## Status

`completed`

## Proof tier

`Governed`

## Dependency

- Depends on: none
- Closure checkpoint: CP0
- Owned requirements: UIR-001, UIR-002, UIR-004, UIR-019, UIR-070, UIR-071, UIR-072, UIR-078

## Objective

Freeze the real branch baseline, build scoped architecture evidence, inventory all agent-chat consumers, and capture the current rendered behavior before production refactoring begins.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Reconcile the live simple-chats branch and record the execution base SHA.
- Load current SharedInfo skills and record hashes.
- Build a healthy scoped CodeAnalytics snapshot covering AgentFramework.Components, Modules.AgentFramework, AppComponents/UI, Processes consumers, and relevant tests.
- Record project inventories, dependency direction, cycles, findings/hotspots, exact definitions, references, representative consumers, CSS, and owner tests.
- Capture current large-desktop UI parity evidence for the representative Agent Chat surfaces and open overlays.
- Classify source drift and decide pass, repair, or block.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `docs/architecture/overview.md`
- `src/UI/README.md`
- `src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor`
- `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

## Required deliverables

- `proof/SB01/source-baseline.md`
- `proof/SB01/sharedinfo-hashes.json`
- `proof/SB01/codeanalytics-snapshot.md`
- `proof/SB01/dependency-evidence.md`
- `proof/SB01/consumer-inventory.md`
- `proof/SB01/css-selector-inventory.md`
- `proof/SB01/test-owner-inventory.md`
- `proof/SB01/ui-baseline.md`
- `proof/SB01/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Validate this prepared bundle before source work.
2. Fetch the branch and compare the actual head with the preparation-time head.
3. Read current SharedInfo skills, especially bundle execution, CodeAnalytics impacted tests, Components MCP, and architecture governance.
4. Build the narrowest healthy architecture snapshot that still covers the cross-project consumer graph.
5. Inspect exact definitions and references for every named component; add newly discovered consumers to the durable inventory.
6. Inventory existing Razor CSS, data-testid values, accessible names, scroll owners, and overlay states for components expected to move or delegate.
7. Resolve real application routes and safe fixture/setup paths from source; do not guess them.
8. Capture named large-desktop baseline screenshots/traces and inspect them.
9. Record drift classification and CP0 decision.

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

- No production diff means impacted-test selection is not required for closure.
- A single focused existing test may be run only to prove the local test environment/discovery when needed; record its expected and actual discovery.
- Do not run unfiltered Components, Playwright, Stable, or full solution tests.

## Browser/UI proof

- Capture normal and open-overlay states at the named large-screen desktop viewport.
- Include Agent catalog/switch, main chat, floating catalog/active chat, settings identity/runtime, and at least one contextual or Process consumer where safely reproducible.
- Record first viewport, scroll owner, focus, overlay layering, clipping, and visible actions.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [x] Live execution base and SharedInfo hashes are durable.
- [x] CodeAnalytics snapshot health is trustworthy.
- [x] All direct consumers and owner tests are inventoried.
- [x] Current dependency direction and cycles are recorded.
- [x] Representative UI baseline is captured and inspected.
- [x] No production source is changed.
- [x] CP0 records pass, repair, or block.

## Do not do

- Do not create the neutral project yet.
- Do not change Razor markup or CSS.
- Do not begin opportunistic cleanup.
- Do not treat a missing symbol as evidence until snapshot health is proven.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `CP0` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
