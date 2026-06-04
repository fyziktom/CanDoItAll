# SB06 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-007, RQ-013.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`.
- Browser proof: N/A because SB06 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` | `0BE679B0FE2AA7BA9550647B2A368C60DFD78D946E499DA4F10F0541D2E62351` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | `592175D917B27889986B1C6E1BE04C3C3DC40ADE74456B9DC10A70CF054AABF1` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | `E0096BD4717B87E50C484EFFE7FDF4427DF95A7FEBA07EBA3CD99CF7E4F4A36B` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs` | `DDE94304AA9B106DC9BF460712D629578982E860598BB59D7B7A762387A201F8` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `71804AC56428CE8614FB757415663116EFF7789B1274DD09E38D81EBCE977180` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs` | `D8B866986FE79E306D29DE16AA9910F5117F6897D79E56BB4F73146ABF8A2260` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | `10E7325A791FDD174E856CF52F9708BE2BED61A69A0F369EE69BB80DD2ECA11F` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `BFCDC27EF45579D83452E4E10D04BEE63B5C7DF80D91F617AF8E6F7A2590053B` |
| `bundle://proof/SB06/source-assertions/dispatcher-execution-client-migration.md` | `6E21C21CA5891F32E237F27443A65980AB760FE92158BF6AB1861C04BC7E1772` |
| `bundle://proof/SB06/semantic-invariants.md` | `7246C352216746D35AA97CC8520E570FE4AA37BB03E00B53193F165CD438C7D9` |
| `bundle://subbundles/06-06-dispatcher-execution-call-migration/README.md` | `8250606AAC992397BD1C9F3361A42F539A4DB3CB44DEED8824F449FA19B31DED` |
| `bundle://reviews/01-execution-report.md` | `96A49BA7035BF7F26C62E11703BFA4404FF654BEC5396DD6AA6CBA388113C29D` |

## Command Transcripts

- Dispatcher migration architecture tests: `bundle://proof/SB06/transcripts/dispatcher-migration-architecture-tests.txt`.
- Facade tests after migration: `bundle://proof/SB06/transcripts/process-automation-execution-client-tests-after-migration.txt`.
- Direct workspace-call scan: `bundle://proof/SB06/transcripts/dispatcher-direct-workspace-call-scan.txt`.
- Execution-client call scan: `bundle://proof/SB06/transcripts/dispatcher-execution-client-call-scan.txt`.
- MAF/Tooling product dependency scan: `bundle://proof/SB06/transcripts/maf-product-dependency-scan.txt`.
- Hash capture: `bundle://proof/SB06/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first transcript: `bundle://proof/SB06/transcripts/dispatcher-direct-call-baseline.failing-first.txt`.
- Passing transcript: `bundle://proof/SB06/transcripts/dispatcher-migration-architecture-tests.txt`.
- Passing transcript: `bundle://proof/SB06/transcripts/process-automation-execution-client-tests-after-migration.txt`.
- Test name: `ProcessAgentExecutionBoundaryArchitectureTests`.
- Test name: `ProcessAutomationExecutionClientTests`.
- Invariant labels: `SB06_INV_001`, `SB06_INV_002`, `SB06_INV_003`.

## Source Assertions

- Dispatcher execution-client migration: `bundle://proof/SB06/source-assertions/dispatcher-execution-client-migration.md`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`.
