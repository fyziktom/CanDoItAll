# Subbundle result — M05

## Anchor

- Repository commit before: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00-M04/C1 working-tree changes
- Dependency mode: package
- Windows host: Windows x64; SDK `10.0.303`; runtime `10.0.11`
- Container host: Docker Desktop Linux containers

## Implemented behavior

Docker host-tool recipes now validate their complete argument contract before runtime resolution or preflight. Boolean and integer values are strict, recipe and generated CLI budgets are bounded, port lists are bounded and range checked, and `logs --since` accepts only a 30-day-bounded Go-style duration or strict RFC3339 timestamp. Unknown and currently unsupported structured arguments fail closed; reserved environment-variable, label, and mount inputs are bounded before rejection, so this change does not broaden the mutation surface.

The database password-file reader now accepts only a small regular non-link file, reads at most 4096 bytes as strict UTF-8, rejects empty and NUL-containing values, and does not place the path or content in its errors. The Compose validator applies restart, logging, resource, health, secret, network, persistence, publication, and least-privilege checks to each service and includes deterministic negative fixtures.

The future `containers` CI job creates a disposable random password file, validates negative fixtures, builds and starts the complete app/database stack, and removes both Compose resources and the secret in an always-run cleanup. Operations documentation now distinguishes disposable-volume password replacement from explicit in-database rotation for preserved volumes.

## Commands and results

| Scope | Result |
|---|---|
| Windows Unit project build after final parser extraction | PASS, 0 warnings/errors |
| Windows Docker/password/CI unit slice | PASS, 62/62 |
| Windows plugin portability integration class | PASS, 2/2 |
| Docker Compose validator, positive and negative fixtures | PASS |
| Package-mode Web Release publish after final source | PASS |
| Isolated app+db build/readiness smoke after final source | PASS; both healthy, HTTP 200, app loopback only, database unpublished |
| CodeAnalytics scoped refresh | PASS, `snap-20260812141334-0b1deb50`; no blocking errors |

## Validation reuse/invalidation

- Invalidated keys: Docker recipe parsing and CLI construction, password-file contract, Compose static validation, future container workflow, Web publish candidate, and M08 integrated Docker candidate.
- Reused evidence: M01 persisted plan semantics, M02 dependency provenance, M03 process ownership, and M04 MCP transport behavior.
- Reason reuse is valid: M05 does not change those contracts and exercises Docker through the existing no-shell process-host boundary.

## Residuals

Environment variables, labels, and mounts remain unsupported recipe arguments. They are bounded and rejected rather than silently accepted; adding them later requires an explicit recipe contract and dedicated security tests.

The scoped architecture snapshot reports existing size findings and a ControlPlane/Persistence module cycle in the Infrastructure project. Neither edge is reachable from the changed password-file configuration type, and no project boundary or registration was added. These non-blocking repository findings are recorded rather than expanded into an unrelated refactor.

## Decision

`GO`

## Next eligible subbundle

M06
