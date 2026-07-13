# Requirement Traceability

## GPTPro Finding Coverage

| Finding | Requirement(s) | Owning subbundle(s) | Planned proof |
| --- | --- | --- | --- |
| F01 | R01, R02, R03 | SB02, SB03 | Exact-step observation tests; runtime receipt fallback tests; structured summary parser tests. |
| F02 | R01 | SB02 | `ObservationReader_WhenTakePerRunWouldHideStep_ExactStepQueryReturnsBlockedObservation`. |
| F03 | R07 | SB04, SB05 | Adapter bypasses normal agent execution for runtime-owned subprocess; bridge state tests. |
| F04 | R08, R13 | SB04, SB05, SB06 | Accepted/repaired/no-go child mapping tests; parent artifact manifest proof. |
| F05 | R04 | SB06 | Applied-result ledger regression test. |
| F06 | R05 | SB06 | Prompt/diagnostic descriptor tests. |
| F07 | R06 | SB06 | Managed content hash stability tests. |
| F08 | R10 | SB07 | Preflight missing/denied/composed tests. |
| F09 | R08, R09, R12 | SB04, SB08 | Typed `prepare-solution-skeleton` contract and manual-skip validation tests. |
| F10 | R02, R03 | SB02 | Blocked packet and no blind retry tests. |
| F11 | R11 | SB04, SB08 | Template loader hard-gate validation tests across affected templates/artifacts. |
| F12 | R14 | All, SB09 | C# architecture gate, source assertions, CodeAnalytics refresh. |

## Requirement To Subbundle Matrix

| Requirement | SB01 | SB02 | SB03 | SB04 | SB05 | SB06 | SB07 | SB08 | SB09 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R01 | Owns inventory | Owns implementation |  |  |  |  |  |  | Verifies |
| R02 | Owns inventory | Owns implementation | Supports |  |  |  |  |  | Verifies |
| R03 | Owns inventory | Owns implementation |  |  |  |  |  |  | Verifies |
| R04 | Owns inventory |  |  |  |  | Owns implementation |  |  | Verifies |
| R05 | Owns inventory | Consumes |  | Supports |  | Owns implementation |  |  | Verifies |
| R06 | Owns inventory |  |  |  |  | Owns implementation |  |  | Verifies |
| R07 | Owns inventory |  |  | Defines contract | Owns implementation |  |  | Applies | Verifies |
| R08 | Owns inventory |  |  | Defines contract | Owns implementation | Supports parent artifact |  | Applies | Verifies |
| R09 | Owns inventory |  |  | Defines model |  |  |  | Owns implementation | Verifies |
| R10 | Owns inventory | Consumes packet |  |  |  |  | Owns implementation | Applies template requirements | Verifies |
| R11 | Owns inventory |  |  | Owns model/validation |  |  |  | Owns template migration | Verifies |
| R12 | Owns inventory |  |  | Defines rule |  |  |  | Owns implementation | Verifies |
| R13 | Owns inventory |  |  | Defines fields | Owns synthesis | Owns content/hash |  | Applies | Verifies |
| R14 | Owns baseline | Applies | Applies | Applies | Applies | Applies | Applies | Applies | Owns closure |
| R15 | Defines current gaps | Adds tests | Adds tests | Adds tests | Adds tests | Adds tests | Adds tests | Adds tests | Owns harness closure |

## Raw Input Closure Plan

| Raw input | Closure path |
| --- | --- |
| User request | Preserved in `inputs/00-original-request.md`; covered by README outcome contract and all subbundles. |
| GPTPro analysis files | Preserved in `inputs/gptpro-analysis-source`; mapped by F01-F12 traceability above. |
| GPTPro codex B01-B07 | Expanded into SB02-SB09; see subbundle source references. |
| GPTPro calculator evidence | SB01 uses as incident characterization; SB05/SB06/SB09 prove product files alone do not satisfy parent evidence. |
| User warning about other templates/artifacts | SB01/SB08 inventory and validation across all subprocess parents and shared artifact templates. |

## Exception Rows

| Item | Exception status | Reason | Follow-up |
| --- | --- | --- | --- |
| Live 5032 blocked process | Partially in scope for implementation | Bundle can require recovery diagnostics/playbook, but local tests may not access live instance. | SB09 must record host/environment proof or explicit blocker. |
| Unrelated OpenAPI package advisory | Out of scope | CodeAnalytics warning is unrelated to process/MAF hardening. | Separate security dependency bundle if needed. |
