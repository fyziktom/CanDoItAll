# Execution order

| ID | Work | Depends on | Checkpoint |
|---|---|---|---|
| SB00 | Baseline and decision lock | - | CP0 |
| SB01 | Canonical model and generic identities | SB00 | - |
| SB02 | Application ports and use cases | SB01 | - |
| SB03 | PostgreSQL store and migration | SB02 | - |
| SB04 | Profile-fenced invocation and provider resolution | SB03 | - |
| SB05 | Operations, idempotency, cancellation, recovery, audit | SB04 | - |
| SB06 | Composition and backend checkpoint | SB05 | CP1 |
| SB07 | HTTP definition and conversation API | SB06 | - |
| SB08 | HTTP turn, operation, cancel and recovery API | SB07 | - |
| SB09 | Focused HTTP/PostgreSQL/OpenAPI proof | SB08 | CP2 |
| SB10 | Documentation, guards and cleanup | SB09 | - |
| SB11 | Final regression and release gate | SB10 | FINAL |

## Unlock rule

A subbundle unlocks only when:

- its dependency is `Completed`;
- the dependency proof manifest is valid;
- any checkpoint after the dependency is `Pass`;
- no reopened finding affects its assumptions.

## Reopen rule

A change to project boundary, canonical model, persistence transaction, profile fence, operation
identity, or API contract reopens the earliest owning subbundle and locks downstream work.
