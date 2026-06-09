# SB015 Proof Manifest

## Status
Completed.

## Objective
Gate E: prove MAF workflow-backed and direct-agent process execution paths.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 MAF/direct-agent subset.
- Critical invariant contract: `bundle://proof/SB015/semantic-invariants.md`
- Downstream dependency: SB016-SB018 deterministic scenario proof may start only after both MAF workflow and direct-agent paths are proven.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `e744a30fe5d5d247125e9385c93cf1f6f724d9a0d1e60b3549b0040a1325f597` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB015/README.md` | `51d53d42de9d69417be70b728c659fc068a27e68c2793f3af7ced96d38b49d50` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB015/transcripts/maf-direct-agent-execution-tests.txt` | `73e98835fbc7474c89d532d4ecb3461afb04d7df93f748c669cd760934596723` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB015/transcripts/maf-direct-agent-source-assertions.txt` | `bbd5428d63a175988538662a91830f6feedbb2884e3bb49a5ea385deb7282301` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB015/red-team/route-enum-only-proof-rejection.txt` | `f4d93bd39c598066a1c821467f7048219504b9369c01d818506fc83781260870` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` | `285684ec66e3070bb670abb8ccbc147aeb018cf98667bf46ddca93697fc9e08f` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs` | `14edc825a9b8e78429ec49f60c551c53a1e1ebddc575552069daca17d1407b91` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` | `ab2380b298fe5fd627fd50106b62be5aedc43192f10ffc72b2e068a766b4cc26` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs` | `07a4816a2fc9d0b7b86d9ad9375bf877afa8f2c36ddf2651d74cfd04e7a2f6ec` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs` | `afc411845803aa758a799a07a64c102b77ce75aee8492c28aa887f62af6479d6` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `ca1b6122dcb41c7fc2cbd015bf322233d7871c1e9f8efc8ddc788cacf167fea7` |

## Command Transcripts
- Integration: `bundle://proof/SB015/transcripts/maf-direct-agent-execution-tests.txt`
- Source assertions: `bundle://proof/SB015/transcripts/maf-direct-agent-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB015/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB015/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team route-enum-only rejection: `bundle://proof/SB015/red-team/route-enum-only-proof-rejection.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Workflow-backed dispatch | `ProcessWorkflowRunCoordinator` | Process runtime state and run detail readback | Starts workflow assignments, maps completion/waiting states, and records workflow links | Human-input workflow test prevents treating every workflow run as completed |
| Direct-agent candidate | `ProcessDispatchCandidateFactory` and direct-agent runtime service | Direct-agent route handlers and finalizer | Preserves binding, recovery, cooperation, tool profile, and executor facts | Route-enum-only red-team rejects enum/source-only proof |
| Fake provider process execution runs | Mock agent runtime catalog and process dispatch | Execution run readback and artifacts | Completed mock provider runs carry provider/model, process run ID, step ID, and outcome | Tests assert completed execution runs and absence of skipped direct-release execution |
| Process-owned finalizer | Direct/workflow route finalizer services | Runtime state, artifact projection, downstream proof | Direct and workflow completions route through one process-owned finalizer path | Source test rejects divergent direct/workflow mutation paths |

## Closure
- Shallow-pass trap: A fake pass could claim MAF/direct-agent support from route enum presence or candidate creation alone.
- Adversarial negative proof: `bundle://proof/SB015/red-team/route-enum-only-proof-rejection.txt`
- Semantic positive proof: `bundle://proof/SB015/transcripts/maf-direct-agent-execution-tests.txt` plus `bundle://proof/SB015/transcripts/maf-direct-agent-source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB015/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: MAF workflow-backed and direct-agent fake-provider execution paths are source-backed; deterministic .NET scenario proof remains owned by SB016-SB018.
