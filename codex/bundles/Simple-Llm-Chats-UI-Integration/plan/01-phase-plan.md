# Phase Plan

| Work unit | Prerequisite | Critical outcome | Downstream invalidation |
|---|---|---|---|
| SB01 | none | trustworthy source/proof/user baseline | all later proof |
| SB02 | SB01 | immutable neutral records and generic actions | SB03, SB05, SB11 |
| SB03 | SB02 | safe role-driven transient transcript | SB05, SB08-SB12 |
| SB04 | SB03 | exact reconnectable active operation | SB05, SB09-SB12 |
| SB05 | SB04 | CP1 hardening/Agent parity | blocks all Simple Chat UI |
| SB06 | CP1 | isolated UI boundary/gateways/auth | SB07-SB12 |
| SB07 | SB06 | definition catalog/editor | SB08-SB12 |
| SB08 | SB07 | canonical conversation workspace | SB09-SB12 |
| SB09 | SB08 | durable streaming/cancel/recovery | SB10-SB12 |
| SB10 | SB09 | activate main page and CP2 | blocks floating integration |
| SB11 | CP2 | unified floating catalog/windows and CP3 | SB12 |
| SB12 | SB11 | one final frozen-commit closure | final user handoff |

## Parallelism

The plan is intentionally mostly serial. UI components within SB07 or SB08 may be developed in parallel only when their file ownership does not overlap and the integration owner merges them before validation. SB02/SB03 and SB10/SB11 must not run concurrently because they share reusable or shell owners.

## Reopen Rules

- Shared presentation contract changes reopen SB02/SB03 and all downstream browser proof.
- Active-operation/application contract changes reopen SB04 and streaming/UI lifecycle proof.
- Route/navigation/composition changes reopen SB06/SB10/SB11.
- Floating contributor or Agent coordinator changes reopen SB11 and targeted Agent context/affinity proof.
- Provider/model option changes reopen SB07 definition editor proof.
