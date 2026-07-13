# Architecture Checkpoints

## Checkpoint A: After SB01

- Current source inventory is complete.
- CodeAnalytics snapshot id, dashboard health, dependency/cycle result, and exact source refs are recorded.
- All nine subprocess parent steps and shared artifact-template audit scope are enumerated.
- No implementation starts if any subprocess parent is missing from inventory.

## Checkpoint B: After SB02 And SB03

- Blocked packet and result-summary contracts are placed in the correct project.
- `ProcessRuntimeProjectionQueryService` and dispatch/rework code delegate packet construction instead of absorbing more conditionals.
- Exact observation query does not create a dependency cycle.
- Unit tests exercise new packet/result-summary behavior without full app host.

## Checkpoint C: After SB04

- Typed subprocess contract model is strongly typed and backward compatible.
- Template loader validation catches missing accepted/no-go metadata and manual skip policy gaps.
- No generic dictionaries or raw `JsonElement` contracts leak into runtime behavior.
- Dependency direction remains acyclic.

## Checkpoint D: After SB05

- Parent bridge implementation lives in focused service(s).
- Adapter partials are thin callers, not final logic owners.
- Accepted/no-go child outputs are validated against typed contract.
- Old generic child folder evidence path is removed or limited to explicitly non-critical compatibility behavior.

## Checkpoint E: After SB06

- Artifact descriptors are semantic and rendered into prompt/diagnostics.
- Produced artifact refs use readback content hashes.
- Ledger uses applied result.
- Tests prove fake slot-only/content-free artifacts fail.

## Checkpoint F: After SB07

- Preflight contracts and implementation do not create runtime-to-module cycles.
- Dispatch blocks before LLM execution for mandatory missing/denied tools.
- Denial diagnostics include tool name, provider/composition/authorization category, process run id, step id, and remediation.

## Checkpoint G: After SB08

- Every subprocess parent has typed contract metadata or explicit exception row.
- Shared artifact templates in scope have typed hard gates or explicit follow-up rows.
- `prepare-solution-skeleton` manual skip cannot bypass required parent evidence.
- Markdown prose no longer carries hard gates that runtime cannot validate.

## Checkpoint H: After SB09

- CodeAnalytics snapshot refreshed.
- Dependency cycles still empty.
- C# architecture review gate passed or has explicit blockers.
- Old large classes are thinner or have source assertions showing new behavior moved out.
- Regression harness covers the original failure class and broader template surfaces.
