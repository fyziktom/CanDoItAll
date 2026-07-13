# Requirement To Subbundle Map

| Requirement | Subbundles |
| --- | --- |
| R01 | SB01, SB05, SB07, SB08, SB27, SB32 |
| R02 | SB02, SB06, SB16, SB20, SB32, SB36, SB37, SB40 |
| R03 | SB02, SB07, SB08, SB10, SB27, SB38, SB40 |
| R04 | SB03, SB09, SB10, SB33 |
| R05 | SB03, SB09, SB27, SB33 |
| R06 | SB03, SB06, SB09, SB21, SB31, SB33 |
| R07 | SB04, SB11, SB12, SB13, SB14 |
| R08 | SB11, SB13, SB20, SB21 |
| R09 | SB15, SB16, SB17, SB19, SB36, SB37, SB40 |
| R10 | SB02, SB16, SB17, SB18, SB33, SB36, SB37, SB40 |
| R11 | SB18, SB19, SB30, SB37, SB40 |
| R12 | SB20, SB21, SB23 |
| R13 | SB22, SB23, SB28 |
| R14 | SB24, SB25, SB29 |
| R15 | SB25, SB26, SB28, SB29 |
| R16 | SB27, SB28, SB29, SB33, SB39, SB40 |
| R17 | SB30, SB33, SB34 |
| R18 | SB31, SB34 |
| R19 | SB32, SB33, SB34, SB40 |
| R20 | SB05, SB10, SB14, SB19, SB23, SB29, SB34, SB35, SB40 |
| R21 | SB04, SB05, SB11, SB12, SB13, SB14, SB15, SB16, SB17, SB18, SB19, SB24, SB29, SB30, SB33, SB34, SB35, SB36, SB37, SB38, SB39, SB40 |
| R22 | SB37, SB40 |
| R23 | SB36, SB37, SB40 |
| R24 | SB37, SB40 |
| R25 | SB36, SB40 |
| R26 | SB37, SB38, SB39, SB40 |
| R27 | SB35, SB36, SB37, SB38, SB40 |
| R28 | SB38, SB40 |
| R29 | SB39, SB40 |

## Repair Execution Status

| Subbundle | Status | Traceability consequence |
| --- | --- | --- |
| SB35 | Completed | Architecture re-entry and characterization gate preserved; authorizes repair evidence only. |
| SB36 | Completed | R02, R04, R09, R10, R20, R25, and R27 implementation ownership plus the SB40 full-suite confirmation passed. |
| SB37 | Completed | R09-R12, R20, R22-R24, and R27 focused plus real-host browser/E2E proof passed. |
| SB38 | Completed | R01, R03, R04, R07, R12, R13, R16, R20, R26-R28 transport/UI/worker proof passed; PostgreSQL distributed leases and browser confirmation are recorded by SB40. |
| SB39 | Completed | R14-R17, R19, R20, R26, R28, and R29 external security/isolation plus live main-driver process proof passed. |
| SB40 | Completed | Terminal builds/tests, browser/runtime, live conformance, static/CodeAnalytics/red-team review, and final closure validation passed through `bundle://proof/SB40/manifest.md`. |
