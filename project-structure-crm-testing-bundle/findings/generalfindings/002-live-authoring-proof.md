# General Finding 002: Live Authoring Proof Covered Multiple Node Types And A Persisted Dependency

## Proven In The Browser

- Right-click radial menu opened on the live canvas.
- A `Note` node was created through the note composer.
- A `Task` node was created through the grouped `Work` path.
- An `AI agent` participant was created through the grouped `People` path.
- A `Phase` node was created directly from the grouped root actions.
- A new user-authored link of kind `1` was then created from `Canvas test phase` to `Canvas scratch task`.

## Evidence

- `artifacts/project-structure-crm-testing/evidence/playwright/b04-rightclick-root-menu-1600.png`
- `artifacts/project-structure-crm-testing/evidence/playwright/b04-scratch-authoring-proof-1600.png`
- B04 structure readback ended at `23` nodes and `23` links, with persisted scratch nodes and the dependency link present.

## Why This Matters

- The test did not stop at viewing an imported plan.
- It proved that the same isolated project can be extended manually after backfill, which is necessary if later work is going to evolve the plan instead of replacing it.
