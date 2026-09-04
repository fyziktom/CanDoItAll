# Validation Plan

The plan was executed against the implemented product. The preparation diagnostic remains root-cause evidence only; closure relies on the focused product tests, builds, frozen stable gate and live direct/shared Ollama evidence below.

## Execution results

| Gate | Result | Durable proof |
| --- | --- | --- |
| Consolidated focused unit | 154/154 passed | `bundle://proof/final-focused-unit.log` |
| Consolidated focused integration | 37/37 passed | `bundle://proof/final-focused-integration.log` |
| Focused component refresh | 5/5 passed | `bundle://proof/final-focused-components.log` |
| Production Release build | Passed, 0 warnings and 0 errors | `bundle://proof/final-production-build.log` |
| Stable test-solution Release build | Passed, 0 warnings and 0 errors | `bundle://proof/final-stable-build.log` |
| Frozen broad stable gate | 9,525/9,526 passed; the one unrelated concurrent-search duration threshold passed immediately in isolation | `bundle://proof/stable-gate.log`; `bundle://proof/provider-history-timing-rerun.log` |
| Live direct Ollama | Succeeded/Committed and canvas refreshed | `bundle://proof/SB06/live/direct-ollama-live-summary.json` |
| Live shared Ollama | Succeeded/Committed and canvas refreshed | `bundle://proof/SB06/live/shared-ollama-live-summary.json` |

## Common focused recipe

For each slice, first build every changed production project listed below. Then build the owning test solution, list the exact filter, verify the stated discovery count, and execute the same filter with --no-build --no-restore. Use Release and /m:1.

```powershell
dotnet build <changed-production-project> --configuration Release /m:1
dotnet test <owning-test-solution> --configuration Release --list-tests --filter "<exact-filter>" /m:1
# Compare actual discovered cases with the expected count recorded below.
dotnet test <owning-test-solution> --configuration Release --no-build --no-restore --filter "<exact-filter>" /m:1
```

If the implementation uses different test names, update this bundle before running; never silently change a filter after seeing results. Data-driven rows count as discovered cases. Zero or unexpected discovery fails.

## V00 — SB00

- Implemented class: CanDoItAll.Tests.Unit.AgentFramework.Maf120UpgradeCompatibilityTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.Maf120UpgradeCompatibilityTests
- Expected: 5 cases: asset description/schema nested-path agreement; malformed asset call remains nonexecuting; native/OpenAI schema parity; ordinary agent failure trace remains observable; dependency graph uses one coherent MAF/MEAI family.
- Workflow mapper filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.MafWorkflowTurnResultMapperTests; expected 3 existing cases. Exact additional workflow cases: MafWorkflowExecutorFailureDiagnosticsTests.Executor_failure_surfaces_root_cause_in_summary_events_and_diagnostic_payload and WorkflowRuntimeLifecycleRedGateTests.ActiveCancellationSignalsBackendToken; expected 2 existing cases.
- A2A filters: FullyQualifiedName~AgentA2AMetadataTests|FullyQualifiedName~AgentA2AHostCardFactoryTests|FullyQualifiedName~A2ARemoteAgentToolFactoryTests; expected 9 existing cases.
- Selected MCP exact cases: MCP_tool_arguments_require_a_JSON_object_for_every_transport, Remote_http_tool_result_reader_unwraps_structured_or_single_json_text, Remote_http_tool_result_reader_rejects_multiple_text_blocks, Remote_http_tool_result_reader_rejects_non_json_text; expected 4 cases.
- Project-structure regression filter: FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureAgentRuntimeToolRoundTripIntegrationTests; expected 12 existing cases.
- Build: restore and build CanDoItAll.slnx in Release /m:1 after inspecting resolved assets. Build the Unit and Integration owning solutions before --list-tests.
- Package assertions: stable MAF 1.20.0; A2A/Hosting preview 1.20.0-preview.260831.1; MEAI 10.9.0; Microsoft.Extensions floor 10.0.11; OpenAI remains 2.12.x; no MAF 1.18/MEAI 10.8 or NU1605.
- The isolated preparation probe is evidence that 1.20 does not fix malformed binding. It is not V00 product proof.

## V01 — SB01

- Implemented class: CanDoItAll.Tests.Unit.AgentFramework.ToolArgumentFeedbackTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.ToolArgumentFeedbackTests
- Expected and discovered: 8 cases covering captured malformed shape/no delegate; corrected nested call/one delegate; safe known binding detail; secret-bearing exception redaction; invalid enum/type rejection; unknown mutation result; supported read-result compatibility; and boolean-schema compatibility.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Unit.slnx.
- Build changed among: AgentFramework.Models, Runtime.Abstractions, Maf.
- Regression filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.MafAgentRuntimeToolInvocationResultTests. Current baseline names 14 exact cases; re-list and require 14 unless intentional rows are added and bundle is updated.

## V02 — SB02

- Implemented class: CanDoItAll.Tests.Integration.AgentFramework.ToolOutcomeCompletionIntegrationTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ToolOutcomeCompletionIntegrationTests
- Expected: 7 cases: failure plus prose; verified same-operation correction; unrelated operation success; unknown commit; pending approval; cancellation; no-tool answer.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Build changed among: AgentFramework.Models, Runtime.Abstractions, Core, Web.
- Additional implemented class: CanDoItAll.Tests.Integration.AgentFramework.ToolOutcomeReceiptApiIntegrationTests; 4 cases cover persisted/public failure, committed-later-failure, redaction and legacy Unknown.
- Use a second exact filter/count for that class. Real persistence and Web response mapping are required.

## V03 — SB03

- Implemented class: CanDoItAll.Tests.Integration.AgentFramework.ScopedPriorToolEvidenceIntegrationTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ScopedPriorToolEvidenceIntegrationTests
- Expected: 8 cases: prior failure included; contradictory prose does not replace it; provider switch parity; foreign project excluded; foreign session/agent/profile excluded; revoked access excluded; fake receipt excluded; deterministic budget priority/truncation.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Build changed among: AgentFramework.Core, Maf, Runtime.Abstractions.

## V04 — SB04

- Implemented class: CanDoItAll.Tests.Integration.AgentFramework.DirectSharedToolTransportParityIntegrationTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.DirectSharedToolTransportParityIntegrationTests
- Expected: 6 cases: complete schema; streamed tool call; sequential correlated results; supported multiple calls; malformed/unmatched result; upstream/cancellation/capability failure.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Fake only the external provider server. The shared case must traverse consumer client, Web source endpoint /api/shared-providers/openai/v1/chat/completions, request policy and relay adapter.
- Build changed among: AgentFramework.Maf, AgentFramework.ProviderManagement, SharedProviders.Http, Composition/Web.
- Existing exact one-case regressions:
  - CanDoItAll.Tests.Unit.SharedProviderRelayPolicyTests.ChatCompletionsSupportedSubset_NormalizesCanonicalRequest
  - CanDoItAll.Tests.Unit.SharedProviderRelayPolicyTests.FunctionToolsAndToolChoice_RoundTripCanonically
  - CanDoItAll.Tests.Unit.SharedProviderRelayPolicyTests.UpstreamFailures_AreSanitizedAndRetryAfterIsBounded
- Existing Ollama filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.OllamaToolResultProtocolHandlerTests; expected 6 baseline cases, re-list before execution.
- Connector/policy regression proof also covers recursive Ollama boolean-schema normalization and the protocol-valid assistant `content: ""` plus `tool_calls` sequence. The normalizer is scoped to the Ollama relay; OpenAI relay payloads remain unchanged.

## V05 — SB05

- Implemented integration class: CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureAssetEffectIntegrationTests.
- Filter and expected: FullyQualifiedName~...ProjectStructureAssetEffectIntegrationTests, 6 cases: valid managed commit/readback; invalid parent no effect; unauthorized/path rejection; analytics fails after commit; cancellation after commit; unknown-before-readback no retry.
- Implemented component class: CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureCommittedEffectRefreshTests.
- Filter and expected: FullyQualifiedName~...ProjectStructureCommittedEffectRefreshTests, 5 cases: success refresh; committed-plus-failed refresh; no-effect no refresh; unrelated project ignored; duplicate/disposed notification.
- Owning solutions: tests/Solutions/CanDoItAll.Tests.Integration.slnx and CanDoItAll.Tests.Components.slnx.
- Build changed among: Modules.Workbench, Modules.AgentFramework, AgentFramework.Core.
- Regression filter: FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureAgentRuntimeToolRoundTripIntegrationTests; baseline 12 cases, re-list and update expected count if intentionally extended.

## V06 — SB06

- Implemented deterministic class: CanDoItAll.Tests.Integration.AgentFramework.ProjectStructureAgentToolIntegrityEndToEndTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ProjectStructureAgentToolIntegrityEndToEndTests
- Expected: 4 cases: malformed/future prose no node and failed status; corrected call/one visible canonical node; next-turn scoped failure; unrelated target success does not resolve.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Real Web/runtime/persistence/project services; fake only the external model boundary.
- The live matrix used the same installed `gemma4-12b-256k` model for direct and shared routes. Both positive runs produced exactly one committed asset effect and an automatic matching-project canvas refresh. The earlier shared attempts supplied realistic correction/failure evidence: boolean JSON Schema nodes were normalized for Ollama, and an empty assistant content string accompanying `tool_calls` was accepted as protocol-valid. No false-success state was accepted.

## Browser proof

SB05/SB06 browser proof used the desktop canvas at 2048×1100 CSS pixels. The shared and direct captures show the existing contextual chat over the primary canvas after the matching committed node became visible without a page reload. The canvas remains the graph navigation/scroll owner; the contextual transcript owns its internal scrolling; no new dialog, textarea or compact/mobile composition was introduced. Evidence: `bundle://proof/SB06/live/ollama-schema-fixed-committed-refresh.png` and `bundle://proof/SB06/live/ollama-shared-schema-fixed-committed-refresh.png`.

## Static and broad gates

Every protected source/test change follows docs/testing.md portability-static: scanner self-tests, complete scan including untracked protected files, review all ADDED/STALE findings, repair defects, refresh only intentional baseline deltas, and final enforcement without --write-baseline.

At the final frozen SB06 checkpoint, run the documented broad stable gate once because root package versions, ToolExecutionReceiptRecord/public execution persistence, and composition are cross-cutting invalidation triggers. If the actual final diff avoids all of those triggers, update this plan with diff evidence before waiving it; do not quietly omit the gate.

Run tools/Validation/Test-Documentation.ps1 after maintained documentation changes and validate this bundle at prepared/completed stages as appropriate.

## Semantic evidence

Governed SB01 and SB03 manifests and semantic invariant contracts are present at `bundle://proof/SB01/` and `bundle://proof/SB03/`. They record raw notes, expected behavior, rejected shallow implementations, failing-first and passing transcripts, changed production sources, production assertions, adversarial cases, downstream checks and SHA-256 hashes.
