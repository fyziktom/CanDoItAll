# SB08 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-009, RQ-013.
- Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.md`.
- Browser proof: N/A because SB08 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://CanDoItAll.slnx` | `861CCCA0AFA6B9E37473C56AA0C136DFDFC63E10C62C46DD350D2FCB09B8BB14` |
| `repo://src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj` | `2C82DAE7A6492E5DC0D99B6B5A5D1C89A4702B892F71757981E46302949D6115` |
| `repo://src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs` | `40F51A118B2A4413A10BF33ED3502A644B3B544BAEC7246C0B4290AF3C8853E2` |
| `repo://src/CanDoItAll.Processes.Contracts/README.md` | `32457A63585A520C4BFD6B04537F7910E9CEBE243AF2EE1FB4AF12F5BFD3AA1A` |
| `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | `D30A02B3BA9F1ABEEB72519B0D30AE9C236DD382620E756FF2F8D2E17A97EAC6` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs` | `8AEE918CD72B2AC8937EE91D3223947E42AA19AC86DEA5E941A7BFA590F262E4` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | `4BE3F50C7BEA44ED2925C5F6AAF0C183F3A5DD95A98DC0631B4320078C63EECD` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `94264F3580B937EC8300EF207985F0E80E8744FC461F83FAE71D086F9233EFC2` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs` | `99E40FE2027EE262BC7B31180299D5DC1A0399A68B19EC80CEE86D62147F4276` |
| `bundle://proof/SB08/source-assertions/contracts-foundation.txt` | `2ECDAC65A24021143F80819213D84368244EF2864BC1FD285D86E3D7959463EA` |
| `bundle://proof/SB08/semantic-invariants.md` | `C40452F0F84D341C5B2F7E34BF992438519FC878C4947AA3BB7E83A997854BC9` |
| `bundle://subbundles/08-08-minimal-process-contracts-foundation/README.md` | `8ECF54C9201C7319167C3F809E9B101FD347177A21D40182082675261D96BDC7` |
| `bundle://reviews/01-execution-report.md` | `5B43953CAFE1DE181CBED85371D390D498762EEED7B2E6C86935F5753EDB10B5` |

## Command Transcripts

- Failing-first baseline: `bundle://proof/SB08/transcripts/contracts-project-absent.failing-first.txt`.
- Restore transcript: `bundle://proof/SB08/transcripts/restore-test-projects.txt`.
- Architecture tests: `bundle://proof/SB08/transcripts/unit-architecture-tests.rerun.txt`.
- Execution-client mapping tests: `bundle://proof/SB08/transcripts/integration-execution-client-tests.txt`.
- Contracts source scan: `bundle://proof/SB08/transcripts/contracts-foundation-source-scan.txt`.
- Contracts neutrality scan: `bundle://proof/SB08/transcripts/contracts-neutrality-scan.txt`.
- Contracts reference neutrality scan: `bundle://proof/SB08/transcripts/contracts-reference-neutrality-scan.txt`.
- Dispatcher neutral request scan: `bundle://proof/SB08/transcripts/dispatcher-neutral-request-scan.txt`.
- No Process Core/driver project scan: `bundle://proof/SB08/transcripts/no-core-driver-project-scan.txt`.
- No entity/view-model contract scan: `bundle://proof/SB08/transcripts/no-entity-viewmodel-contracts-scan.txt`.
- Hash capture: `bundle://proof/SB08/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first: `bundle://proof/SB08/transcripts/contracts-project-absent.failing-first.txt`.
- Passing transcript: `bundle://proof/SB08/transcripts/unit-architecture-tests.rerun.txt`.
- Passing transcript: `bundle://proof/SB08/transcripts/integration-execution-client-tests.txt`.
- Passing transcript: `bundle://proof/SB08/transcripts/contracts-neutrality-scan.txt`.
- Test name: `ProcessAgentExecutionBoundaryArchitectureTests`.
- Test name: `ProcessAutomationExecutionClientTests`.
- Invariant labels: `SB08_INV_001`, `SB08_INV_002`, `SB08_INV_003`.

## Source Assertions

- Contracts foundation: `bundle://proof/SB08/source-assertions/contracts-foundation.txt`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`.
