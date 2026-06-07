# SB015 Proof Manifest

## Scope
- Subbundle: SB015 - Gate E - pre-execution/start-transition parity.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionRouteFacts.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterializationSideEffects.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Source inspected: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteExecutionModels.cs`, and the changed source files above.
- Changed tests: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Bundle status files: `bundle://inventories/02-source-hotspots.md`, `bundle://reviews/01-execution-report.md`, `bundle://subbundles/SB015/README.md`, `bundle://proof/SB015/semantic-invariants.md`.

## Changed File Hashes
| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionRouteFacts.cs` | `0FD4A66DBD993AA3CDC4D5B9F23365CF12EFFD3D4CEF32E748DCABE1BB859AFC` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs` | `31F38E82A8338BA8A097021C6473755B5EAC0DA51667431E280FBCFB390646B6` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` | `378A86C6B73DC1BCE27FBE9E8DD55E7AB89AFAF75138E8DF7AFF7064BE679105` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs` | `85948B01D66AB4790211563ECE290C3761CB551A1C5E8785906F25CBEF6F9948` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterializationSideEffects.cs` | `227987736DC4E0885C57FA85A3AA1577AF4717B53CA50E7341C1A7BA7A4B1E18` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs` | `80A339F55DCAE142B9999929BF324E607D05C1433D22128F627E54400E118F65` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `132C932717AC789B2D21F3F5C5114EBD02DB0514B80FDDCBFE8F714D86EFE655` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `A379E4294A6AD8A03AB06B30177B461880AF49D5C62FE2A524D6EC55842890AB` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `8AA822B160B5DE0559F02E1A8D54B534F9368A5CE6FF55A2E4A59AEAB8C7A912` |
| `bundle://inventories/02-source-hotspots.md` | `D85E090C1E0B897A9C6DEBDD51E2334BC0A212AECBC7BD6C7E280DA774767FCB` |
| `bundle://reviews/01-execution-report.md` | `D3B0B1EB4B82560F6733F2052A31B8BD5466FDC6FE38485E8D4E832E4D1DF872` |
| `bundle://subbundles/SB015/README.md` | `EF703201A4C42732B2968AEA0DE319BF3E019261C3F1233656EF65C3A1AA2086` |
| `bundle://proof/SB015/semantic-invariants.md` | `2E52A475380A854897D27A0C2A64AAA99DF9D4FA907275A4E01C3DF8568832D8` |

## Command Transcripts
- Critical solution build: `bundle://proof/SB015/transcripts/critical-build.txt`
- Passing unit architecture proof: `bundle://proof/SB015/transcripts/pre-execution-start-transition-unit-tests.txt`
- Passing focused integration behavior proof: `bundle://proof/SB015/transcripts/pre-execution-start-transition-integration-tests.txt`
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions-and-scans.txt`
- Anti-stub audit: `bundle://proof/SB015/transcripts/source-assertions-and-scans.txt`
- No-Core/no-driver scan: `bundle://proof/SB015/transcripts/source-assertions-and-scans.txt`
- Hash proof: `bundle://proof/SB015/transcripts/changed-file-hashes.txt`
- Failing-first proof: N/A - process refactor with no behavior change; behavioral negative proof is captured by `StartTransitionRouteHandler_SB015_INV_001_preserves_reload_and_continue_candidates_behavior`, and source negative proof is captured by `bundle://proof/SB015/transcripts/source-assertions-and-scans.txt`.

## Semantic Invariants
- Contract: `bundle://proof/SB015/semantic-invariants.md`
- Invariant ID: `SB015-INV-001`
- Test name: `StartTransitionRouteHandler_SB015_INV_001_preserves_reload_and_continue_candidates_behavior`
- Test name: `ProcessDispatchStartTransitionPlanner_SB10_INV_001_builds_start_request_without_executing_transition`
- Test name: `ProcessDispatchStartTransitionPlanner_SB10_INV_002_preserves_fresh_skip_wrapper_parity`
- Test name: `ProcessDispatchRoutePlanner_SB11_INV_001_classifies_database_upstream_and_recovery_routes_without_side_effects`
- Test name: `ProcessDispatchDatabaseRequirementBlocker_SB05_INV_001_preserves_status_targets_and_transition_shape`
- Test name: `ProcessMissingUpstreamArtifactMaterializationFacts_SB07_INV_001_selects_only_missing_runnable_agent_source`
- Test name: `ProcessMissingUpstreamArtifactMaterializationFingerprint_SB09_INV_001_is_order_stable_and_target_sensitive`
- Test name: `ProcessMissingUpstreamArtifactRerunRequestBuilder_SB12_INV_001_preserves_rerun_fields_and_directive_scope`
- Test name: `Process_core_contract_candidate_driver_readiness_SB013_INV_001_uses_pre_execution_route_facts_without_source_payload`
- Test name: `Process_core_contract_candidate_driver_readiness_SB014_INV_001_separates_materialization_pure_rules_from_side_effects`

## Source Assertions
- `ProcessDispatchRouteServices` builds `ProcessDispatchPreExecutionRouteFacts` from the route candidate before database and upstream materialization decisions.
- `ProcessDispatchPreExecutionGuardHandler` consumes route facts for database decisions, missing-upstream planning, block request construction, and materialization side-effect coordination.
- `ProcessMissingUpstreamArtifactMaterialization` contains pure missing-upstream facts, blocker, fingerprint, and rerun request builders with no EF, scope, logger, serializer, or rerun side-effect tokens.
- `ProcessMissingUpstreamArtifactMaterializationSideEffects` owns duplicate journal detection, journal persistence, replay-context serialization, scoped `ProcessesService` resolution, rerun requests, and logging.
- `ProcessDispatchRouteHandlers` still builds the start-transition request, attempts the claim transition, reloads after claim failure, returns `ContinueCandidates` for unusable reloads, and updates the route context for the same `InProgress` candidate.
- `ProcessRunAutomationDispatchService.Dispatch` honors `ProcessClaimedDispatchResult.ContinueCandidates` by continuing the candidate loop.
- Source scans found no Process Core project, no production process driver API, no route-services source-payload adapter construction, no stubs, and no UI/mobile proof drift beyond N/A runtime-service documentation.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumers | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessDispatchPreExecutionRouteFacts` | `ProcessDispatchPreExecutionRouteFacts.FromCandidate(candidate)` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` | `ProcessDispatchPreExecutionGuardHandler.BuildDatabaseRequirementDecision`, `PlanMissingUpstreamArtifactMaterialization`, `BuildMissingUpstreamArtifactBlockTransitionRequest`, and `RecordAndRequestMissingUpstreamArtifactMaterializationAsync` | Built from the current route candidate for a single pre-execution decision and not persisted, published, or reused across dispatch claims. | `Process_core_contract_candidate_driver_readiness_SB013_INV_001_uses_pre_execution_route_facts_without_source_payload`, `Process_core_contract_candidate_driver_readiness_SB014_INV_001_separates_materialization_pure_rules_from_side_effects`, and `StartTransitionRouteHandler_SB015_INV_001_preserves_reload_and_continue_candidates_behavior` in `bundle://proof/SB015/transcripts/`. |

## Gate Result
- Entry gate: Passed after SB014 closure.
- Closure gate: Passed with critical build, focused unit architecture proof, focused integration behavior proof, source assertions, anti-stub audit, no-Core/no-driver scan, hash proof, semantic invariant contract, and production behavior artifact matrix.
- Downstream dependency check: SB016-SB033 may proceed with pre-execution database blocking, upstream materialization, start-transition reload, and `ContinueCandidates` behavior guarded before subprocess, direct-agent execution, projection, validation, pure-rule, driver-readiness, and final red-team phases continue.
