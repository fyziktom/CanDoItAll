# Target solution

This bundle aims for a **module-local process dispatch application layer** that is almost ready for a future Core extraction.

## Target shape after this bundle

```text
ProcessRunAutomationDispatchService
  - public DispatchAsync facade
  - composition only
  - owns DI dependencies until later module split

Module-local services / coordinators
  - Claim lifecycle coordinator/store
  - Route pipeline and route handlers
  - Candidate hydration service
  - Direct-agent binding service
  - Pre-execution guard/materialization service
  - Subprocess runtime service
  - Subprocess artifact projection service/store
  - Transition/finalizer application service
  - Failure/exception closure coordinator
  - Static-helper/rule boundary cleanup helpers

Core readiness output
  - matrix of pure candidates
  - matrix of side-effect candidates
  - explicit blockers still preventing Core extraction
```

## What remains intentionally out of scope

```text
CanDoItAll.Processes.Core project creation
IProcessDriverPack / driver registry / production driver packages
public process driver API
UI/mobile/browser proof
feature changes or behavior changes
```

## Why this helps future drivers

Drivers should eventually attach to stable process intents, not to dispatcher internals. This bundle does not create driver APIs, but it names future driver-readiness seams:

- execution intent
- evidence intent
- projection intent
- materialization intent
- subprocess delegation intent
- route decision intent
- finalization intent

These remain documentation-only until the module-local boundaries are stable.
