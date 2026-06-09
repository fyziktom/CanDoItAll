# SB018 Proof Manifest

## Status
Completed.

## Objective
Gate F: prove deterministic .NET process scenario completion.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 deterministic .NET scenario subset.
- Critical invariant contract: `bundle://proof/SB018/semantic-invariants.md`
- Downstream dependency: SB019-SB021 live OpenAI policy/proof may start only after deterministic non-live process execution is proven.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `cd2c08b434dc80e89cea4b51bb601696328af74642e574ea6f66fbe15bae898b` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB018/README.md` | `d24066aab64c3915f588434b95436738b2b6baebd839501405c936d1c165544e` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB018/transcripts/dotnet-process-scenario-tests.txt` | `18bbfe323197cdefad9fc1a03d69bc62d3afb851551e5d2e8a8ee950dcd507b1` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB018/transcripts/dotnet-process-scenario-source-assertions.txt` | `2486cd42e5a4eb52a8e6d33ac0df6ced1bc75275618c1f3daed3fdda9ccfbc2c` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB018/red-team/generic-artifact-only-proof-rejection.txt` | `a3bb1e63937d21f5167cb052b90417648de4cc5f1f1503a3fec4285f607e2ab5` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs` | `07a4816a2fc9d0b7b86d9ad9375bf877afa8f2c36ddf2651d74cfd04e7a2f6ec` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs` | `14edc825a9b8e78429ec49f60c551c53a1e1ebddc575552069daca17d1407b91` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | `fc4c5fb9161f92c1ef961cd79ce6679ca6057ac13e0ea06a77eac534ec3cfc3a` |

## Command Transcripts
- Integration: `bundle://proof/SB018/transcripts/dotnet-process-scenario-tests.txt`
- Source assertions: `bundle://proof/SB018/transcripts/dotnet-process-scenario-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB018/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB018/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team generic-artifact-only rejection: `bundle://proof/SB018/red-team/generic-artifact-only-proof-rejection.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `MockApp/ValidationEngine.cs` | Mock direct-agent/process execution | Managed file readback and downstream QA | Created during deterministic mock process and modified during repair path | Generic-artifact-only red-team rejects proof without concrete file signals |
| Implementation change set | Developer mock agent/process route | Artifact projection and QA artifact handoff | Recorded as required artifact and managed file | Tests assert exact artifact title/path/content signals |
| Migration/rollout checklist | Developer mock agent/process route | Artifact projection and QA artifact handoff | Recorded as required artifact and managed file | Tests assert DB-free rollout content |
| Completed process run and steps | Durable outbox mock process E2E | Runtime readback and later UI/browser proof | All expected run steps complete, skipped branch is skipped, and artifacts are recorded | Gate rejects generic artifact records without run/step completion |

## Closure
- Shallow-pass trap: A fake pass could cite generic artifact rows without proving .NET-specific file creation/modification and managed artifact readback.
- Adversarial negative proof: `bundle://proof/SB018/red-team/generic-artifact-only-proof-rejection.txt`
- Semantic positive proof: `bundle://proof/SB018/transcripts/dotnet-process-scenario-tests.txt` plus `bundle://proof/SB018/transcripts/dotnet-process-scenario-source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB018/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Deterministic .NET create/modify scenario proof is source-backed; live provider smoke is separately governed by SB019-SB021.
