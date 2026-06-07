# Requirements And Hard Constraints

## Functional preservation

- Preserve all dispatch behavior.
- Preserve all route-stage behavior.
- Preserve all finalizer behavior.
- Preserve all retry/recovery/provider behavior.
- Preserve all subprocess runtime and projection behavior.
- Preserve artifact projection, validation, satisfaction, lineage, and external reference behavior.

## Architecture constraints

- No Process Core project in this bundle.
- No production driver APIs in this bundle.
- No UI or browser-proof work.
- Use module-local models, adapters, and services only.
- Pure rule candidates may be prepared and tested, but not moved to a new Core project.

## Proof constraints

- 36 separate subbundle rows must be recorded in the execution report.
- Critical gates must be recorded after every phase.
- Every critical gate must include source scan, build/test proof, shallow-pass trap, and red-team notes.
- The final report must include a Core readiness decision and a driver readiness decision.
