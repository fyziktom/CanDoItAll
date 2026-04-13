# Publication, versioning, and clone-engine decomposition

## Status

- `Ready`

## Objective

- Separate publication lifecycle concerns from graph cloning, harden version/slug conflict handling, and remove publication-era dependence on legacy compatibility paths.

## Covered Inputs

- `U003` Architecture and DB conflict concerns.
- `BRQ-009` Publish/version hardening.
- `F005` Publish/version race windows.

## Prerequisites

- `07-architecture-review-gate-b` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessDeletionIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs

## Deliverables

- A separated publication service or equivalent internal responsibility split.
- A separated clone engine or equivalent internal responsibility split.
- Version/slug conflict handling that is race-aware and translated cleanly.
- Publish/clone tests proving canonical dependency behavior still survives draft creation.

## Dependency Impact

- Runtime and read-side work should no longer depend on a coupled publish/clone monolith.
- Gate C will inspect whether this decomposition is real or superficial.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extract publication lifecycle logic from graph clone logic while preserving the public entrypoint as needed.
2. Harden version-number and slug-allocation behavior so conflicts are handled explicitly rather than left to late surprises.
3. Ensure clone behavior uses the canonical dependency model and does not reintroduce legacy-field semantics except through the approved compatibility boundary.
4. Update publish, delete, and relevant MCP or integration tests to reflect the new responsibility split.

## Scope Exceptions

- This phase does not yet extract runtime transition logic.
- This phase may keep the public `ProcessesService` façade if that minimizes churn.

## Do Not Do

- Do not keep lifecycle and clone logic coupled in one broad method because it already works.
- Do not rely on `Max + 1` versioning without conflict protection.
- Do not backfill legacy dependency fields inside the clone path unless the compatibility boundary explicitly owns that behavior.

## Acceptance Checklist

- Publication lifecycle and clone logic are responsibility-separated.
- Version and slug conflict handling is explicit and race-aware.
- Publish/draft creation still preserves canonical dependency behavior.
- Delete behavior remains correct after the split.

## Proof Required

- Integration tests for publish, delete, and clone behavior.
- Proof that publish conflict failures are translated cleanly.
- Execution-report note describing the separated responsibilities.

## Browser Validation Logging

- N/A.

## Progression Gate

- Publication/version behavior is decomposed and conflict-aware, and draft cloning no longer reintroduces hidden legacy dependency semantics.

## Suggested Agent Prompt

```text
Implement only subbundle 08. Separate publication lifecycle from graph cloning, harden version and slug conflict handling, keep the public service façade stable if helpful, and stop before runtime or query decomposition.
```
