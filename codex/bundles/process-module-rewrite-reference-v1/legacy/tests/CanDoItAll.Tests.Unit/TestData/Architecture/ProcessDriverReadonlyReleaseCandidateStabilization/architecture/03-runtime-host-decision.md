# Runtime Host Decision Matrix

## Current Decision
Runtime host remains `Not approved`.

Current decision: all runtime-host surfaces are `Not approved`.

Contract line: `v1.x verification-only alpha`.

## Not Approved Surfaces
- Runtime host
- Driver registry
- Runtime selector
- DI registration
- Manager command
- Scheduler hook
- Workflow hook
- Execution-capable driver
- File/network/storage/workspace/process mutation

## Required Before Reconsideration
- Durable audit persistence design and tests.
- Authorization and approval model.
- Lifecycle ownership, cancellation, timeout and retry semantics.
- Sandbox, connector and command allow-list policy.
- Failure semantics for partial failure and duplicate requests.
- Public API version governance.
- Red-team negative proof for runtime-host approval claims.

## Canonical Matrix
The complete current approval matrix is `bundle://architecture/04-runtime-host-decision.md`.

## This Bundle
This bundle may update roadmap and prerequisite tests only. It must not implement runtime host behavior.
