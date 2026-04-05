# Hard gates

The following gates are **non-negotiable** before the plugin wave is allowed.

## HG-1 - Remove persisted parallel truth

- No cross-module projection nodes may be persisted into `Workbench_ProjectObjects`.
- No read flow may call `SyncGraphAsync`-style materialization into canonical tables.

## HG-2 - Establish the universal node carrier + typed facets/bindings model

- Node remains central and stable.
- X/Y and markers remain canonical.
- External artifact/media/storage/provider/resource/secret bindings are removed from the carrier and moved to typed binding/facet ownership.

## HG-3 - Introduce a central node-kind registry and capability matrix

- Kind/family/allowed-relations/allowed-party-roles/editor-schema/transition rules must come from one registry.
- CRM/HR node scope and page/editor logic must consume the registry instead of hardcoded switch logic.

## HG-4 - Introduce node transition history

- Reclassification may not silently replace semantic meaning in place.
- Stable node identity is required, but transition history and facet supersession must exist.

## HG-5 - Remove editable hierarchy dual-write

- Editable hierarchy must have one canonical containment representation.
- Generic relation tables may not duplicate containment.

## HG-6 - Replace the closed provider/resource seam with a descriptor-driven connector platform

- New connectors must not require enum expansion.
- First-party providers/resources should register through the same descriptor/manifest seam.

## HG-7 - Add hard closure enforcement

- Architecture guardrail tests must exist.
- `scripts/gate_check_phase7.py` must pass on the target branch.
- No item can be closed by ADR text alone.
