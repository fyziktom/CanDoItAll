# SB021 Proof Manifest

## Scope
- Subbundle: SB021 - Gate G - execution/retry/provider parity.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs`.
- Source inspected: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionAttemptLoopFacade.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAssignedAgentProviderRepairCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`, and the changed source files above.
- Changed tests: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Behavioral tests inspected and run: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Bundle status files: `bundle://inventories/02-source-hotspots.md`, `bundle://reviews/01-execution-report.md`, `bundle://subbundles/SB019/README.md`, `bundle://subbundles/SB020/README.md`, `bundle://subbundles/SB021/README.md`, `bundle://proof/SB021/semantic-invariants.md`.

## Changed File Hashes
| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs` | `EC3AE537D40CE5B798E0A194377C2358C07C5D3F3DB5AC9EF630367C836BB958` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs` | `7931A4D3284190A01B65EF3434A28755DBEB9B43DBA5767390FF41719B642072` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs` | `14EDC825A9B8E78429EC49F60C551C53A1E1EBDDC575552069DACA17D1407B91` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs` | `245E50E448FDE238C150A252F3A8874C05F1D44729CDD5663A289A210A45FC82` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs` | `AA46B672DBFE774FDEB24F8432D3B2371D01B9D95BD01C3BF27EFB8EB3CD223D` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs` | `3FDBF5714ED7DC824D01F4C1C7DF67395E1BA5E98EDFAD2704C3DBFCCAF64FB9` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs` | `7F8F3EF2B89128BEBAA1B1263154054385531FA15299F7ABF8FAB32AAC44FC7C` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` | `57E053FF4E04449E5370EFD706DA2A63DE6884326584A346BE019902F2C43BF7` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `D8512334A596CCC7F3887E19F28FF77BEC246314981CAFBA3B27C80E8B8D30F0` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs` | `78D238BA26FCE22B7AFD198594688DD50BEEDC1A54CBE77EE513DC113CFD3633` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `DC4F648E6B3B63D6F1315EA6554D455DE0EEFC179DBBB7464FA634A50D52354A` |
| `bundle://inventories/02-source-hotspots.md` | `0C2AF3519DC98D8AF60CC6A0767D7755563CB450206CFDF3923F735841922019` |
| `bundle://reviews/01-execution-report.md` | `AE760894B99821EEE499A641D1F45625EA8687D3E999CD4D1715E32452845C5F` |
| `bundle://subbundles/SB019/README.md` | `B06D914467D88AE41D1B716B7D45DE6E4ED315400C0653EE7C374A2318446177` |
| `bundle://subbundles/SB020/README.md` | `520783FE926F2B471B70F3A92F5AB7BEB17FAB005038B05FAF674FD5F5690D3F` |
| `bundle://subbundles/SB021/README.md` | `BB2F5FFBC098800EE07F97C476FFAB4EE89728BB0E671D6EE021EFBA0EEF2102` |
| `bundle://proof/SB021/semantic-invariants.md` | `7CCE849137191E53CA74E2BE5B29E5F763F5F8FFA1217465DF10A6D42A727E92` |

## Command Transcripts
- Critical solution build: `bundle://proof/SB021/transcripts/critical-build.txt`
- Passing focused unit architecture proof: `bundle://proof/SB021/transcripts/execution-boundary-unit-tests.txt`
- Passing focused integration behavior proof: `bundle://proof/SB021/transcripts/execution-retry-provider-integration-tests.txt`
- Source assertions: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`
- Anti-stub audit: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`
- No-Core/no-driver scan: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`
- Hash proof: `bundle://proof/SB021/transcripts/changed-file-hashes.txt`
- Failing-first proof: N/A - process refactor with no behavior change; behavioral negative proof is captured by `Process_core_contract_candidate_driver_readiness_SB021_INV_001_preserves_execution_retry_provider_and_finalizer_paths`, retry/no-progress/provider integration tests, and source negative proof in `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`.

## Semantic Invariants
- Contract: `bundle://proof/SB021/semantic-invariants.md`
- Invariant ID: `SB021-INV-001`
- Test name: `Process_core_contract_candidate_driver_readiness_SB019_INV_001_moves_direct_agent_runtime_to_execution_input_model`
- Test name: `Process_core_contract_candidate_driver_readiness_SB020_INV_001_slims_route_execution_outcome_to_run_snapshot`
- Test name: `Process_core_contract_candidate_driver_readiness_SB021_INV_001_preserves_execution_retry_provider_and_finalizer_paths`
- Test name: `ProcessAutomationExecutionRunSelection_SB06_INV_001_selects_latest_current_attempt_competing_run`
- Test name: `ProcessAutomationExecutionRunSelection_SB06_INV_002_preserves_stale_and_approval_blocking_rules`
- Test name: `ProcessAutomationExecutionRunSelection_SB06_INV_003_preserves_completion_and_fresh_recovery_skip_rules`
- Test name: `HasPriorNoProgressRetrySignal_SB09_INV_001_detects_repeated_fingerprint_after_restart`
- Test name: `ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_that_only_missed_required_tools`
- Test name: `ShouldRetryIncompleteSuccessfulRun_compresses_repeated_no_progress_missing_tool_attempt`
- Test name: `ShouldRetryIncompleteSuccessfulRun_returns_true_for_provider_failure_that_returned_no_text`
- Test name: `ShouldRetryRecoverableFailedRun_returns_true_for_provider_transport_failure_on_non_implementation_step`
- Test name: `OrderFallbackProviders_excludes_ollama_from_process_recovery_fallbacks`
- Test name: `ProcessDispatchFinalizerAdapter_SB009_INV_001_preserves_route_dto_context_parity_and_apply_conditions`

## Source Assertions
- `ProcessDispatchDirectAgentExecutionInput` carries route candidate, trigger, and lease renewal into the direct-agent runtime boundary.
- `ProcessDispatchDirectAgentExecutionAdapter` is the single direct-agent execution compatibility edge that converts to dispatcher candidate and back to route execution outcome.
- `ProcessDispatchDirectAgentRuntimeService` contains no dispatcher candidate/outcome or route-model adapter conversion tokens.
- `ProcessRouteExecutionOutcome` carries `ProcessRouteExecutionRunSnapshot` instead of full execution detail for route consumers.
- `ProcessDispatchCompetingExecutionGuardService` uses `executionOutcome.ExecutionRun.Id` for competing execution selection and does not convert route outcomes back to dispatcher outcomes.
- Direct-agent finalizer input still flows through `ProcessDispatchFinalizerAdapter`, which converts to dispatcher execution outcome before building `ProcessDispatchFinalizerContextFactory.ForDirectAgent`.
- Retry, no-progress duplicate detection, no-progress observed/compressed journals, provider repair decisions, provider fallback directive construction, and provider recovery journal persistence remain present in the execution loop and collaborators.
- Source scans found no Process Core project, no production process driver API, no direct-agent runtime adapter drift, no route-boundary full-detail drift, no stubs, and no UI/mobile proof drift beyond N/A runtime-service documentation.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumers | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessDispatchDirectAgentExecutionInput` | `DirectAgentExecutionRouteHandler` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs` | `ProcessDispatchDirectAgentRouteService`, `ProcessDispatchDirectAgentRuntimeService`, and `ProcessDispatchDirectAgentExecutionAdapter` | Built from the current route candidate, trigger, and lease-renewal delegate for one direct-agent execution path; not persisted or published. | `Process_core_contract_candidate_driver_readiness_SB019_INV_001_moves_direct_agent_runtime_to_execution_input_model` and `Process_core_contract_candidate_driver_readiness_SB021_INV_001_preserves_execution_retry_provider_and_finalizer_paths` in `bundle://proof/SB021/transcripts/execution-boundary-unit-tests.txt`. |
| `ProcessDispatchDirectAgentExecutionAdapter` | `CreateDirectAgentRuntimeService` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `ProcessDispatchDirectAgentRuntimeService` | Instantiated with the existing dispatcher execution method and used as the single direct-agent execution compatibility edge; not persisted or exposed as a public contract. | `Process_core_contract_candidate_driver_readiness_SB019_INV_001_moves_direct_agent_runtime_to_execution_input_model` and source negative scans in `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`. |
| `ProcessRouteExecutionRunSnapshot` | `ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs` | `ProcessDispatchCompetingExecutionGuardService` and `CompetingExecutionGuardRouteHandler` | Built from the dispatcher execution run id for one route execution outcome; not persisted or used to replace full finalizer detail. | `Process_core_contract_candidate_driver_readiness_SB020_INV_001_slims_route_execution_outcome_to_run_snapshot`, `Process_core_contract_candidate_driver_readiness_SB021_INV_001_preserves_execution_retry_provider_and_finalizer_paths`, and `ProcessAutomationExecutionRunSelection_SB06_INV_001_selects_latest_current_attempt_competing_run` in `bundle://proof/SB021/transcripts/`. |

## Gate Result
- Entry gate: Passed after SB020 closure.
- Closure gate: Passed with critical build, focused unit architecture proof, focused integration behavior proof, source assertions, anti-stub audit, no-Core/no-driver scan, hash proof, semantic invariant contract, and production behavior artifact matrix.
- Downstream dependency check: SB022-SB033 may proceed only while direct-agent execution input, route execution run snapshots, retry/no-progress/provider repair behavior, competing execution guard behavior, and direct-agent finalizer detail parity remain guarded before projection/validation DTO, pure-rule, driver-readiness, scorecard, smoke, and final red-team phases continue.
