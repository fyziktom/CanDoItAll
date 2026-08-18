# SharedInfo architecture skills used during preparation

The preparation reviewed the current skills under
`CanDoItAll.SharedInfo/codex/skills`.

## Bundle workflow

### `bundles/candoitall-bundle-preparation`

Applied conclusions:

- preserve source input;
- separate current-state evidence, architecture, plan, work units, and reviews;
- build explicit traceability;
- use dependency-safe subbundles;
- provide machine-readable status and proof expectations.

### `bundles/candoitall-bundle-execution`

Applied conclusions:

- one unlocked subbundle at a time;
- proof manifests and session handoffs;
- no silent deviations;
- checkpoints control downstream unlocks;
- governed versus behavioral proof tiers.

### `bundles/candoitall-csharp-architecture-bundle-guard`

Applied conclusions:

- checkpoints review the entire current feature block;
- project boundaries and dependency direction are explicit;
- partial-class growth and service-location are rejected;
- later subbundles reopen when an earlier invariant changes.

## Architecture review

### `csharp-architecture-governor`

Applied conclusions:

- project boundaries follow real dependency force;
- patterns require actual variability or policy force;
- no generic manager/service-locator abstraction;
- behavior and lifecycle must be directly testable.

### `architecture-reviews/feature-block-architecture-review`

Applied conclusions:

- review definition, transcript, provider execution, persistence, API, lifecycle, and tests as one
  feature block;
- do not optimize only a single class or endpoint.

### `architecture-reviews/canonical-model-review`

Applied conclusions:

- one canonical simple-chat definition model;
- one canonical definition revision snapshot;
- one canonical transcript model;
- deterministic adapters around existing generic conversations;
- compatibility paths remain explicit and quarantined.

The executor must load the current skill versions before implementation.
