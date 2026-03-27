
# Cross-cutting risks and mitigations

## Risk 1 — Schema fragmentation

**Problem:** New fields are added ad hoc in multiple places.  
**Mitigation:** Centralize on the metadata strategy from I01.

## Risk 2 — Duplicate registries

**Problem:** Repositories, providers, scripts, or secrets get re-modeled inside Workbench even though reusable modules already exist.  
**Mitigation:** Every item folder lists reusable modules and file references that should be inspected first.

## Risk 3 — Browser-impossible actions

**Problem:** UI exposes actions such as opening a native terminal without a realistic implementation path.  
**Mitigation:** Normalize these requirements into app-hosted execution surfaces.

## Risk 4 — Visual regressions hidden behind green tests

**Problem:** Canvas changes may still look wrong even when tests pass.  
**Mitigation:** Screenshot evidence is mandatory for UI items.

## Risk 5 — Intermittent Prompt Factory duplication bug appears fixed but is not

**Problem:** The 44-node bug may disappear temporarily without a real root-cause fix.  
**Mitigation:** Require diagnostics, regression tests, and explicit root-cause evidence.

## Risk 6 — Over-scoping participant notes into full CRM

**Problem:** People features can explode into an unrelated CRM project.  
**Mitigation:** Keep the implementation explicitly lightweight and registry-based.

## Risk 7 — Toolbox UX divergence

**Problem:** Prompt Factory and Project Structure evolve separate floating toolbox systems.  
**Mitigation:** Implement I20 first and reuse it in I21 and I23.
