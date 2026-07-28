# Requirement Traceability

| Requirement | Summary | Owner | Required evidence | Gate |
|---|---|---|---|---|
| R01 | Capture 1.13 baseline and fixtures | SB01 | proof/SB01/fixtures; package/build baseline | A1 |
| R02 | Exact stable/preview package train | SB02, SB07 | package graph; A2A smoke | A4 |
| R03 | Centralize two MAF version values | SB02 | `src/MAF/MicrosoftAgentFramework.Packages.props`; alignment test | SB02 |
| R04 | Preserve runtime isolation/preload architecture | SB01, SB04, SB06, SB08 | lifecycle map; concurrency tests | A4 |
| R05 | Keep binding and prove approval security | SB03, SB06 | attack matrix; tool policy regression | A2 |
| R06 | Handle 1.13 pending approval state | SB03, SB05, SB08 | native-session rejection; drain/reissue; rollout | A2/A4 |
| R07 | Bind a decision to the complete current pending snapshot | SB03 | atomic persistence and changed-snapshot tests | A2 |
| R08 | No random approval IDs | SB03 | fail-closed tests | A2 |
| R09 | Stage mixed-tool behavior change | SB02, SB03, SB08 | parity option; optional feature gate | A2/A4 |
| R10 | Correct terminal handoff output | SB04 | six-path fixture | A3 |
| R11 | Preserve message/tool ordering | SB04 | merge/history matrix | A3 |
| R12 | Cross-version session/checkpoint compatibility | SB01, SB03, SB05 | fixtures; restore tests | A3 |
| R13 | Preserve custom file/capability security | SB01, SB06 | inventory and security suite | A4 |
| R14 | Resolve Harness/FileAccess usage | SB01, SB06 | discovery classification | A4 |
| R15 | A2A matching preview and smoke | SB01, SB02, SB07 | package graph and hosted smoke | A4 |
| R16 | Inventory optional features | SB01, SB07, SB08 | decision register and follow-ups | A4 |
| R17 | Narrow warning suppressions | SB01, SB02, SB07, SB08 | warning baseline/delta/final | A4 |
| R18 | Preserve finalizer governance | SB01, SB04, SB05, SB08 | trigger comparison and regression | A3/A4 |
| R19 | Structured session persistence diagnostics | SB03, SB05 | telemetry and failure tests | A3 |
| R20 | Canary and rollback | SB01, SB03, SB05, SB08 | backup, flags, rehearsal | A4 |
| R21 | English source comments and conventions | All | review | A4 |
| R22 | Complete execution report | All | reviews/01-execution-report.md | A4 |
