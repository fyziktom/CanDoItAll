# SB06 Proof Manifest

## Status
Completed.

## Owned Requirements And Notes
- REQ-006: Use runtime-host and dry-run readback against real `ProcessRun` and `StepRun` ids produced by automation-dispatched template execution.
- Raw note: Continue toward a generic process-driver runtime host without enabling execution-capable side effects.

## Semantic Contract
- `bundle://proof/SB06/semantic-invariants.md`

## Changed File Hashes
| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` | `CF9722E4BF59777F2E9A5A3C6E3C4C833664FACB78AAD4EC977063A956CE9CDE` | `D0C5401E9B00A8A280FF9AA04B861928C2BDC1192310137FB8E54F9E18824D53` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs` | `NEW` | `7594BBCB64D091A8F23A1CE4A8C776DFDE8EA06D5DB16E7AEEDB9A827BC6AAB3` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs` | `3B528925810D5D0AC2963056126E15E84D203078318C6C1C7AE5B709C8A81588` | `63BC267E63E50684CC08A19E6CB6F7CE1E75F896B68C17BE0176725BB2590AF0` |

## Command Transcripts
- Passing proof: `bundle://proof/SB06/transcripts/focused-test.txt`
- Source assertions: `bundle://proof/SB06/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- Boundary scan: `bundle://proof/SB06/transcripts/boundary-scan.txt`
- Failing-first proof: `bundle://proof/SB06/transcripts/failing-first-source-assertion.txt`

## Semantic Proof
- Test name: `Process_runtime_host_readback_SB06_INV_001_uses_real_process_run_and_step_ids_without_mutation`
- Shallow-pass trap: DTO-only readback or synthetic ids would not prove the manager facade and dry-run mapper work against real process lifecycle ids.
- Semantic positive proof: `bundle://proof/SB06/transcripts/focused-test.txt`
- Source proof: `bundle://proof/SB06/transcripts/source-assertions.txt`

## Downstream Decision
SB07 can proceed. Runtime-host readback now uses ids from completed automation-dispatched process steps and keeps execution-capable mutation blocked.
