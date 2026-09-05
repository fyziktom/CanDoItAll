# Behavior and architecture guard hygiene

## Preservation before movement

Each risky responsibility has a behavior matrix row: baseline evidence, owner, invariant,
positive and negative case, affected consumers, exact test topic, result, and rollback.
Preserve meaningful assertions while changing the harness incrementally with the extraction.

Classify observed behavior as:
- Preserve: intended current observable behavior.
- Isolation safeguard: boundary/lifetime protection needed to preserve behavior across the
  newly controlled seam; no incidental product redesign.
- Unresolved defect: record reproduction and effect; do not silently change it or assert
  it as desired behavior. Resolve before moving the dependent responsibility.

Existing counts are discovery baselines, not coverage budgets or permanent invariants.
Tests may split, consolidate, move layers, or add meaningful cases with explicit mapping.
Expected named cases or numeric discovery must be recorded before execution; zero or
unexpected discovery is invalid proof.

## Evidence layers

| Layer | Proves |
|---|---|
| Pure policy/state tests | Deterministic decisions and transitions |
| Real component + narrow fakes | Rendering and interaction through public seams |
| Production workflow/adapter tests | Real load/mutation/failure semantics |
| Real composition smoke | Production registrations and callers reach real operations |
| Browser interactions + inspected screenshots | Host/lifecycle/focus/assets/layout behavior |
| Dependency/type graph review | Durable direction and transitive isolation |

None substitutes for the others where its claim is required. A fake that echoes desired
results cannot prove production persistence or effect timing. Characterization should
precede each risky change; do not defer all test repair until final cleanup.

## Durable and temporary guards

Keep behavior tests and justified project/type dependency guards. Reflection over public
component metadata can enforce injection categories, but inspect constructors, descendants,
and public signature graphs too. A parent-only check cannot prove subtree isolation.

Remove incidental private-field/method reflection, uninitialized concrete services, exact
partial counts, filename snapshots, and dependency-number quotas. Temporary migration
checks may prove moved calls left the old class; label and retire them after closure.

ProjectStructurePageArchitectureTests' exact 22-partial assertion is a known example for
the owning future child, not permission to change unrelated tests during Agents work.

## Validation cost and UI

Each child selects focused owning tests and downstream consumers, with invalidation keys.
Run the stable aggregate only at a named composition/build/persistence checkpoint, not
because a phase or proof tier is large. Portability-static remains mandatory for protected
source changes, including reviewed baseline deltas and final no-write enforcement.

Application proof uses the named large-desktop viewport. Preserve current composition;
inspect normal and open-overlay states, first viewport, scroll ownership, focus, long labels,
clipping, and constrained containers. Test interactions as well as taking screenshots.
Reusable basic BaseLib changes have their own small/medium/large proof when explicitly owned.
