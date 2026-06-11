# SB04 Proof Manifest

## Status
Completed.

## Owned Requirements And Notes
- REQ-004: Prove the multi-team `software-delivery` template through automation dispatch with inherited role assignments, subprocess artifact handoff, QA/browser proof, release approval, and post-release learning.
- Raw note: Restore reliable template process execution for multi-team development.

## Semantic Contract
- `bundle://proof/SB04/semantic-invariants.md`

## Changed File Hashes
| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` | `E4C12C04755A2EBCA2CB2891BB7AFA6C2A47A0BA215C0FDC312F890531AE6C43` | `4A651E1DC0ED2FE5A49D1C42F78DDFE302EFED8F8EA2CBFE95190F6527362AF2` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs` | `3B528925810D5D0AC2963056126E15E84D203078318C6C1C7AE5B709C8A81588` | `63BC267E63E50684CC08A19E6CB6F7CE1E75F896B68C17BE0176725BB2590AF0` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | `16F8DFA4156D59BBE7EDA09F97A5825872926850BDDE4D29ACE7048C5EBE0672` | `972CD726784CA35C5FE8C503A4128DECD4B42AE961B7E8C16983251CDDE7D07D` |
| `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs` | `3CB3B76F8836C6BE4A1C2602C509BB0CB91442770686B9F2E7FB0780C178D7BF` | `2F1AFBF05010EC296362DDA0FE6DB6732C46693FE8ED2F2159C7FB5BAD2C7695` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `2D45F2F1A8D09C6CAA9F225CB4368604E5E1D86E887F607E6FB8607C4B8577CC` | `3063253823FC5131C749027AA8CE39FBA005847BFEB8B7A95746DA22B9C64C82` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `EA6E29DC1FBFD307BB2F8A4CE7E3806C3887962587844037011CCE6FF29123C6` | `F2727B1EAE9731A3BA352561E9B05EA702B04CF18E61DF1014E130F468945C5F` |

## Command Transcripts
- Passing proof: `bundle://proof/SB04/transcripts/focused-test.txt`
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Boundary scan: `bundle://proof/SB04/transcripts/boundary-scan.txt`
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`

## Semantic Proof
- Test name: `Software_delivery_template_SB04_INV_001_completes_multi_team_governance_through_automation_dispatch`
- Shallow-pass trap: proving only template catalog mapping would miss inherited subprocess agents, cross-kind artifact projection, provider-native browser evidence, screenshot writeback, release approval, and skipped repair branch behavior.
- Semantic positive proof: `bundle://proof/SB04/transcripts/focused-test.txt`
- Source proof: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Regression proof: process-mock direct tests, subprocess projection mapping, and screenshot writeback artifact validation passed in `bundle://proof/SB04/transcripts/focused-test.txt`.

## Downstream Decision
SB05 can proceed. The representative multi-team path now completes through production automation dispatch without manual transition suppression.
