# SB01 Proof Manifest

## Changed Files

| File | SHA256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` | `BC2A2F4ABDA1E492B49CC685B81D3984E079D0F56A87D38692D032FBC603DFA0` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs` | `984CE2100DFAA4B38119243B25AA90F54D1E7FE8ABFBF6D1B6983ED4D36DE4DB` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | `1843860A19D19555B144FB1D5C9014F2F52FF2E42A2C6828E7D7394361E47A12` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `8E7AAA4E49916739944919056B3DB529AFDF4222A1C07FDAED0877A88A1ACAF5` |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle |
| --- | --- | --- | --- |
| Managed process output product path | Current-run workspace tool receipt | Implementation proof validator | Read from execution receipts, normalized, admitted only when under process output product root |
| Browser evidence reference path | Browser tool output or scoped browser evidence ref | Artifact projection and missing-artifact validator | Classified by path shape and expected browser tool kind |

## Validation

- `bundle://proof/SB01/transcripts/targeted-tests.txt`
- `bundle://proof/SB02/transcripts/targeted-tests.txt` for full class confirmation
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-live-db.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/targeted-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
