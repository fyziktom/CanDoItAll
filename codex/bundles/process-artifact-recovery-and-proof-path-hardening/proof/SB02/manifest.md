# SB02 Proof Manifest

## Changed Files

| File | SHA256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `24FCC769BCE6B4ED606F480C3858D34E8652BE7167777A434C9DC7FA17058031` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` | `BC2A2F4ABDA1E492B49CC685B81D3984E079D0F56A87D38692D032FBC603DFA0` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs` | `F90532B82BB772DC487FCDAE050CD5A808413310DA31CC6D9C0297681E18ED7D` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs` | `847FB0ABFA8A4BF0714510967374958A5AB6F08F13E7D2F9950057DBBF63C6DD` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `8E7AAA4E49916739944919056B3DB529AFDF4222A1C07FDAED0877A88A1ACAF5` |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle |
| --- | --- | --- | --- |
| Missing upstream artifact input | Dispatch candidate artifact input resolver | Process automation dispatcher | Blocks downstream dispatch before agent run |
| Upstream materialization directive | Process automation dispatcher | Producing source step agent | Sent through targeted rerun directive with source step context |
| Reopened downstream step | Runtime progression planner | Automation recovery worker/outbox | Source completion reactivates blocked dependent when block reason matches missing upstream artifact |

## Validation

- `bundle://proof/SB02/transcripts/targeted-tests.txt`
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-current-behavior.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/targeted-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
