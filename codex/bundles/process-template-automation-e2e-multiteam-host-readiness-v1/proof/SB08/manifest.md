# SB08 Proof Manifest

## Status
Completed.

## Owned Requirements And Notes
- REQ-008: Run the release matrix, code-first ratio guard, large-screen applicability review, and red-team scans.
- Raw note: Keep the bundle code-first and close fake-proof resistance.

## Semantic Contract
- `bundle://proof/SB08/semantic-invariants.md`

## Changed File Hashes
| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs` | `3B528925810D5D0AC2963056126E15E84D203078318C6C1C7AE5B709C8A81588` | `63BC267E63E50684CC08A19E6CB6F7CE1E75F896B68C17BE0176725BB2590AF0` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.PromptArtifacts.cs` | `NEW` | `29A984DC4D09616C0D9F1C8AC4811C4B92B858EC6CD3A8517D493B96A3F34C56` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.SessionState.cs` | `NEW` | `5F1941A639932B8B08B9C1CD51367BB2E55DB2FD5C8D8746AB8CC7E46690ADBF` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.BranchOutcomes.cs` | `NEW` | `0B2AF5C65E789E7C1FBF11B38D53432A60AB4DE682F765611B83035F4B923F8C` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | `68BA8C3E45D60F52532430D281CE97FA41DB317AA0303E0B46828C694B63DBAC` | `2E26F49903EC61823981D74672B2AB7C1FFEB82B5D0FF310BF15DB3641E737B5` |

## Command Transcripts
- Release matrix: `bundle://proof/SB08/transcripts/focused-test.txt`
- Source assertions: `bundle://proof/SB08/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`
- Boundary scan: `bundle://proof/SB08/transcripts/boundary-scan.txt`
- Red-team scan: `bundle://proof/SB08/transcripts/red-team-scan.txt`

## Semantic Proof
- Test matrix: solution build, code-first guard suite, and representative integration matrix passed after the runtime split.
- Shallow-pass trap: green template E2Es alone would not prove code-first ratio, Core boundary, red-team scans, or oversized runtime-file closure.
- Semantic positive proof: `bundle://proof/SB08/transcripts/focused-test.txt`
- Source proof: `bundle://proof/SB08/transcripts/source-assertions.txt`

## Downstream Decision
Bundle closure can proceed. Live OpenAI smoke and large-screen browser proof were not required because no UI route was changed and no explicit live-model opt-in was provided.
