# No-Core Cutline

A Process Core split is still deferred.

## Why

The projection system still depends on:
- dispatcher nested models,
- dispatcher wrappers,
- module-local storage and workspace services,
- projection-side candidate mutation,
- runtime-specific artifact/proof semantics.

## What this bundle may do

- Split module-local facet implementations.
- Reduce dispatcher service forwarding.
- Add source scans and architecture tests that make a future core split safer.
- Document future driver-readiness vocabulary.

## What this bundle must not do

- Create `CanDoItAll.Processes.Core`.
- Move process runtime behavior to a new project.
- Create driver pack APIs.
