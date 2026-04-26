# Phase Plan

## Execution Order

1. `01-live-run-forensics-and-single-agent-proof`
2. `02-required-artifact-contract-and-prompt-hardening`
3. `03-retry-routing-and-upstream-artifact-recovery`
4. `04-mock-agent-failure-matrix`
5. `05-three-agent-simplified-process-proof`

## Subbundle Dependency Map

```mermaid
flowchart TD
    SB01["01 Live-run forensics and single-agent proof"]
    SB02["02 Required artifact contract and prompt hardening"]
    SB03["03 Retry routing and upstream artifact recovery"]
    SB04["04 Mock-agent failure matrix"]
    SB05["05 Three-agent simplified process proof"]

    SB01 --> SB02
    SB01 --> SB03
    SB02 --> SB04
    SB03 --> SB04
    SB04 --> SB05
```

## Critical Subbundles

- `01` is a critical foundation because it establishes the actual failure classification and the single-agent proof boundary.
- `02` is a critical foundation because all later retries and process proof depend on correct artifact obligation behavior.
- `03` is a critical foundation because retrying the wrong owner makes downstream process proof untrustworthy.

## Phase Gates

| Phase | Entry gate | Closure gate | Downstream impact |
| --- | --- | --- | --- |
| 01 | DB and source references available read-only. | Single-agent proof or focused failing test captures the current behavior. | Blocks all later implementation if diagnosis is wrong. |
| 02 | Phase 01 classification accepted. | Required artifact prompt/projection tests pass, including DB-free checklist. | Unlocks mock matrix and simplified process proof. |
| 03 | Phase 01 identifies current vs upstream ownership cases. | Retry-routing tests pass for current-step and upstream missing artifacts. | Prevents false recovery loops in phase 05. |
| 04 | Phases 02 and 03 have stable contracts. | Mock runtime can reproduce and recover observed failure modes. | Provides deterministic proof substrate for phase 05. |
| 05 | Mock matrix is green. | Three-agent process proof passes and records artifact handoff. | Closes bundle without relying on the full rich process. |

## Execution Policy

- Do not run the full rich software-delivery process as the first validation.
- Use focused integration tests first.
- Use Playwright only when UI/operator state is materially changed or for the final three-agent proof.
- If a later phase finds missing artifact classification is wrong, reopen phase 02 or 03.
