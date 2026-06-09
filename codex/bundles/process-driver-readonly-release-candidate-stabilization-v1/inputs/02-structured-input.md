# Structured Input

## Raw Notes
- Original request is preserved in `bundle://inputs/00-original-request.md`.
- Source artifacts to recheck are preserved in `bundle://inputs/01-source-artifacts.md`.

## Normalized Work Type
- Initiative profile: release-candidate stabilization for the read-only process driver layer.

## Hard Constraints
- No runtime host, registry, selector, DI registration, manager command, scheduler/workflow hook, shell execution, package restore, Office/Graph calls, file/network/storage/workspace read/write, process mutation, claim mutation, transition mutation, finalizer application, retry scheduling, or Core dependency on driver packages.

## Validation Expectations
- Prepared validator must pass before implementation.
- Each subbundle must pass entry and closure gates.
- Critical gates require artifact-backed semantic proof.
- Completed validator and final red-team audit must pass before closure.
