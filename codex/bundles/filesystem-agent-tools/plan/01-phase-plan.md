# Phase Plan

## Execution Order

1. `subbundles/01-01-filesystem-service-capabilities`
2. `subbundles/02-02-runtime-tool-provider-wiring`
3. `subbundles/03-03-tests-and-runtime-proof`

## Subbundle Dependency Map

```mermaid
flowchart LR
    SB01["SB01 Filesystem Service Capabilities"] --> SB02["SB02 Runtime Tool Wiring"]
    SB02 --> SB03["SB03 Tests And Runtime Proof"]
```

## Critical Subbundles

- `SB01`: critical foundation. It creates the extracted owner and proves path policy remains in the service boundary.
- `SB02`: critical foundation. It exposes the new tool names through policy and templates without duplicate or unapproved tools.
- `SB03`: closure. It proves the implementation and architecture claims.

## Phase Gates

- SB01 closes only after direct extracted-plugin tests pass.
- SB02 closes only after catalog/template/composition tests pass.
- SB03 closes only after builds, focused tests, and architecture review are recorded.
