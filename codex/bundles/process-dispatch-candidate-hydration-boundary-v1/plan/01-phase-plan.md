# Phase Plan

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 Entry audit] --> SB02[SB02 Inventory]
  SB02 --> SB03[SB03 Seam design]
  SB03 --> SB04[SB04 Gate A]
  SB04 --> SB05[SB05 Header selector foundation]
  SB05 --> SB06[SB06 Header selector migration]
  SB06 --> SB07[SB07 Hydration read snapshot]
  SB07 --> SB08[SB08 Gate B]
  SB08 --> SB09[SB09 Artifact input assembler]
  SB09 --> SB10[SB10 Branch/dependency context]
  SB10 --> SB11[SB11 Assignment/workflow route]
  SB11 --> SB12[SB12 Gate C]
  SB12 --> SB13[SB13 Technical-agent binding coordinator]
  SB13 --> SB14[SB14 Binding migration]
  SB14 --> SB15[SB15 Recovery query boundary]
  SB15 --> SB16[SB16 Gate D]
  SB16 --> SB17[SB17 Driver readiness map]
  SB17 --> SB18[SB18 Final red-team]
```

## Critical Subbundles

- SB04: first architecture guard before production movement.
- SB08: proves candidate header selection and hydration read snapshot parity.
- SB12: proves candidate assembly parity for artifact inputs, branch outcomes, assignments, workflow route.
- SB16: proves side-effectful binding/recovery boundaries and runtime smoke.
- SB18: final closure and next cutline.

## Phase Gates

| Gate | After subbundle | Must prove |
| --- | --- | --- |
| Gate A | SB04 | no-core/no-driver guardrails, no UI proof drift, inventory/source baseline. |
| Gate B | SB08 | header selector parity, read snapshot build/tests, no lifecycle behavior drift. |
| Gate C | SB12 | candidate assembly parity across subprocess/workflow/direct-agent route kinds. |
| Gate D | SB16 | technical-agent binding side effects preserved, recovery query parity, full build. |
| Final | SB18 | completed validator, full red-team, next cutline. |

## Browser Validation

Expected `N/A` for every subbundle. This bundle is service/runtime only. If UI changes unexpectedly, stop and record a scope violation; only large desktop/PC proof is allowed after explicit justification.
