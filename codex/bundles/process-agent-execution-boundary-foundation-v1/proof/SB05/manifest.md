# SB05 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-006, RQ-013.
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`.
- Browser proof: N/A because SB05 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs` | `272C02989A5F1DC982E7F2FC8F9FD7F1F8CC335265519FC1D07A2305CF983EE5` |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `31A6FFB6C64025D6A839929E211CEA36BD4AD8F2E3DA1D6CF298A63D42D2B677` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs` | `45A66390F356E81CA11BABC805E61234B8BEE5E7A2092FB97BBA69BA684AF2D1` |
| `bundle://proof/SB05/source-assertions/facade-foundation.md` | `D653372C8F7F1EEC31B62F9DD9AA62E71827ED678ECCD30CC780B55D6DD75AE8` |
| `bundle://proof/SB05/semantic-invariants.md` | `36E6544C0A05E680D8360A1A6C39748BCBBB0FB725262DBE4EA59530AA03F103` |
| `bundle://subbundles/05-05-process-automation-execution-client-foundation/README.md` | `1F25949D0AFB9E7705C596E8450D67061A0A471C962052517F20B923B53359F1` |
| `bundle://reviews/01-execution-report.md` | `3C5873940EFC7D430BEA73F722278B184BF2B4FE4908765FBF9AF96FD48A5C0B` |

## Command Transcripts

- Targeted facade tests: `bundle://proof/SB05/transcripts/process-automation-execution-client-tests.txt`.
- Failing-first targeted run: `bundle://proof/SB05/transcripts/process-automation-execution-client-tests.failing-first.txt`.
- Facade and registration scan: `bundle://proof/SB05/transcripts/facade-source-registration-scan.txt`.
- Dispatcher direct-call baseline after SB05: `bundle://proof/SB05/transcripts/dispatcher-direct-call-baseline-after-sb05.txt`.
- Hash capture: `bundle://proof/SB05/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first transcript: `bundle://proof/SB05/transcripts/process-automation-execution-client-tests.failing-first.txt`.
- Passing transcript: `bundle://proof/SB05/transcripts/process-automation-execution-client-tests.txt`.
- Test name: `ProcessAutomationExecutionClientTests`.
- Invariant labels: `SB05_INV_001`, `SB05_INV_002`, `SB05_INV_003`, `SB05_INV_004`.

## Source Assertions

- Facade foundation: `bundle://proof/SB05/source-assertions/facade-foundation.md`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`.
