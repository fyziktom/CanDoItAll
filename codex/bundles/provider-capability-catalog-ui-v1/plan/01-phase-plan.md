# Phase Plan

## Phase Sequence

1. Run prepared-stage validation for this bundle.
2. Execute `SB01` to repair provider catalog parity, local Ollama seed, provider tags, and provider tree UI.
3. Execute `SB02` after provider tag metadata exists; implement capability tags, tree agent selection, filters, card grid, and details dialog.
4. Execute `SB03` after capability save/edit contracts are proven; implement wizard from imagegen proposals and ASCII layouts.
5. Run targeted tests/build, then browser proof for providers and capabilities at a large desktop viewport with dialogs/wizard open.
6. Complete raw-note closure and run completed-stage validation.

## Subbundle Dependency Map

```mermaid
gantt
title Provider and capability catalog UI dependency map
dateFormat  YYYY-MM-DD
section Provider foundation
SB01 Provider catalog parity and tags :crit, sb01, 2026-05-30, 1d
section Capability workspace
SB02 Capability tree filters and details :crit, after sb01, 1d
section Setup flow and proof
SB03 Wizard and visual proof :crit, after sb02, 1d
Final tests and closure validation :after sb03, proof, 1d
```

- `SB01` unlocks provider count/list parity and the shared tag model.
- `SB02` depends on capability tag persistence and saves metadata used by the wizard.
- `SB03` depends on capability edit/save behavior and provides creation flows.

## Critical Subbundles

- `SB01` is a critical data/UI foundation because later filters and catalog counts depend on durable tags and the correct provider source.
- `SB02` is a critical UI/data foundation because the wizard and future tag workflows depend on capability metadata persistence and editable detail contracts.
- `SB03` is critical UI closure because it adds new catalog objects through a multi-step dialog and must be visually verified against proposals.

## Phase Gates

- Gate after preparation: `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/provider-capability-catalog-ui-v1 --profile initiative --stage prepared --repo-root .`
- Gate before `SB01`: confirm `AgentsHomePage` still renders the Workspace provider panel and `ProviderProfile`/`CapabilityCatalogItem` still lack tags.
- Gate after `SB01`: provider tab uses AgentFramework source, local Ollama exists, provider tags persist, and provider tree count matches the badge source.
- Gate after `SB02`: capability filters/cards/details pass targeted tests and component/browser checks.
- Gate after `SB03`: wizard creates MCP/Skill records, upload maps to inline skill config, and screenshots show no overflow/clipping.
- Gate before closure: update proof manifests, raw-note closure, execution report, and run completed-stage validator.
