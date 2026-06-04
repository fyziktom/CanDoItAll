# SB02 Proof Manifest

- Status: Completed.
- Owned requirements: RQ-003, RQ-013.
- Inventory artifact: `bundle://inventories/02-agentframework-usage-in-processes.md`.
- Browser proof: N/A because SB02 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://inventories/02-agentframework-usage-in-processes.md` | `F9400AC148ABC2007CCB6BECFFD8823FE954C39E58A09C147562798096E6B388` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` | `2F510294170DB406DDA2918E336D2A926DBA633A5BB80F82925FCC1F2E315A7B` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | `A2AA6DD900AE9034DEF22E311C19148157DB6261A9666411BFDB9F260B7B8867` |

## Command Transcripts

- AgentFramework usage scan: `bundle://proof/SB02/transcripts/agentframework-usage-scan.txt`.
- Direct execution call scan: `bundle://proof/SB02/transcripts/direct-execution-call-scan.txt`.
- Dispatcher partial line counts: `bundle://proof/SB02/transcripts/dispatcher-partial-line-counts.txt`.
- Hash capture: `bundle://proof/SB02/transcripts/hashes.txt`.

## Source Assertions

- Inventory closure: `bundle://proof/SB02/source-assertions/inventory-closure.md`.

## Failing-First And Passing Proof

- Failing-first: N/A - no production behavior changed in this process inventory gate.
- Passing transcript: `bundle://proof/SB02/transcripts/direct-execution-call-scan.txt`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.
