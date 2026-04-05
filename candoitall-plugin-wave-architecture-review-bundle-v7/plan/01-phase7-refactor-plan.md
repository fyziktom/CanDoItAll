# Phase7 refactor plan

## Phase 0 - Freeze the plugin wave

Do not start the large email / LinkedIn / custom API connector wave yet.

## Phase 1 - Remove persisted parallel truth

- delete SyncGraph-style persistence into canonical Workbench tables
- introduce read assembly contributors
- keep canonical tables project-authored only

## Phase 2 - Establish lean carrier plus typed facets/bindings

- slim the universal carrier
- preserve stable node identity
- preserve X/Y and markers as canonical semantics
- move foreign bindings and kind-specific payload out

## Phase 3 - Introduce the node-kind registry

- create registry/descriptors
- route create/edit/reclassify/UI/CRM-HR capability checks through it
- remove scattered subtype/role switch logic

## Phase 4 - Add node transition history

- add explicit transition journal
- add facet supersession/migration rules
- update tests to verify history

## Phase 5 - Remove editable hierarchy dual-write

- keep one canonical containment truth
- keep semantic relation table only for non-containment edges

## Phase 6 - Build the connector descriptor platform

- replace the closed provider/resource extensibility seam
- migrate existing providers/resources as first-party descriptors

## Phase 7 - Add hard closure enforcement

- add architecture guardrail tests
- run `scripts/gate_check_phase7.py`
- no plugin wave until the hard gates pass
