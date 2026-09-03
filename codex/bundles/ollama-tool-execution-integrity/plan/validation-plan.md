# Validation Plan

No product test ran during preparation. The diagnostic probe in bundle analysis used existing Release assemblies, a fake HTTP handler, and a no-op tool delegate; it is root-cause evidence only.

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

- Planned new class: CanDoItAll.Tests.Unit.AgentFramework.Maf120UpgradeCompatibilityTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.Maf120UpgradeCompatibilityTests
- Expected: 5 cases: asset description/schema nested-path agreement; malformed asset call remains nonexecuting; native/OpenAI schema parity; ordinary agent failure trace remains observable; dependency graph uses one coherent MAF/MEAI family.
- Workflow filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.MafWorkflowTurnResultMapperTests; expected 3 existing cases. Add exact hard-error and cancellation cases to this class only if absent, then update expected discovery before running.
- A2A filters: FullyQualifiedName~AgentA2AMetadataTests|FullyQualifiedName~AgentA2AHostCardFactoryTests|FullyQualifiedName~A2ARemoteAgentToolFactoryTests; expected 9 existing cases.
- Selected MCP exact cases: MCP_tool_arguments_require_a_JSON_object_for_every_transport, Remote_http_tool_result_reader_unwraps_structured_or_single_json_text, Remote_http_tool_result_reader_rejects_multiple_text_blocks, Remote_http_tool_result_reader_rejects_non_json_text; expected 4 cases.
- Project-structure regression filter: FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureAgentRuntimeToolRoundTripIntegrationTests; expected 10 existing cases.
- Build: restore and build CanDoItAll.slnx in Release /m:1 after inspecting resolved assets. Build the Unit and Integration owning solutions before --list-tests.
- Package assertions: stable MAF 1.20.0; A2A/Hosting preview 1.20.0-preview.260831.1; MEAI 10.9.0; Microsoft.Extensions floor 10.0.11; OpenAI remains 2.12.x; no MAF 1.18/MEAI 10.8 or NU1605.
- The isolated preparation probe is evidence that 1.20 does not fix malformed binding. It is not V00 product proof.

## V01 — SB01

- Planned new class: CanDoItAll.Tests.Unit.AgentFramework.ToolArgumentFeedbackTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.ToolArgumentFeedbackTests
- Expected: 7 cases: captured malformed shape/no delegate; corrected nested call/one delegate; safe known binding detail; secret-bearing exception redacted; invalid enum/type rejected; unknown mutation result; supported read-result compatibility.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Unit.slnx.
- Build changed among: AgentFramework.Models, Runtime.Abstractions, Maf.
- Regression filter: FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.MafAgentRuntimeToolInvocationResultTests. Current baseline names 14 exact cases; re-list and require 14 unless intentional rows are added and bundle is updated.

## V02 — SB02

- Planned new class: CanDoItAll.Tests.Integration.AgentFramework.ToolOutcomeCompletionIntegrationTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ToolOutcomeCompletionIntegrationTests
- Expected: 7 cases: failure plus prose; verified same-operation correction; unrelated operation success; unknown commit; pending approval; cancellation; no-tool answer.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Build changed among: AgentFramework.Models, Runtime.Abstractions, Core, Web.
- Additional planned class: CanDoItAll.Tests.Integration.AgentFramework.ToolOutcomeReceiptApiIntegrationTests; expected 4 cases: persisted/public failure, committed-later-failure, redaction, legacy Unknown.
- Use a second exact filter/count for that class. Real persistence and Web response mapping are required.

## V03 — SB03

- Planned new class: CanDoItAll.Tests.Integration.AgentFramework.ScopedPriorToolEvidenceIntegrationTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ScopedPriorToolEvidenceIntegrationTests
- Expected: 8 cases: prior failure included; contradictory prose does not replace it; provider switch parity; foreign project excluded; foreign session/agent/profile excluded; revoked access excluded; fake receipt excluded; deterministic budget priority/truncation.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Build changed among: AgentFramework.Core, Maf, Runtime.Abstractions.

## V04 — SB04

- Planned new class: CanDoItAll.Tests.Integration.AgentFramework.DirectSharedToolTransportParityIntegrationTests.
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

## V05 — SB05

- Planned integration class: CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureAssetEffectIntegrationTests.
- Filter and expected: FullyQualifiedName~...ProjectStructureAssetEffectIntegrationTests, 6 cases: valid managed commit/readback; invalid parent no effect; unauthorized/path rejection; analytics fails after commit; cancellation after commit; unknown-before-readback no retry.
- Planned component class: CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureCommittedEffectRefreshTests.
- Filter and expected: FullyQualifiedName~...ProjectStructureCommittedEffectRefreshTests, 5 cases: success refresh; committed-plus-failed refresh; no-effect no refresh; unrelated project ignored; duplicate/disposed notification.
- Owning solutions: tests/Solutions/CanDoItAll.Tests.Integration.slnx and CanDoItAll.Tests.Components.slnx.
- Build changed among: Modules.Workbench, Modules.AgentFramework, AgentFramework.Core.
- Regression filter: FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureAgentRuntimeToolRoundTripIntegrationTests; baseline 10 cases, re-list and update expected count if intentionally extended.

## V06 — SB06

- Planned deterministic class: CanDoItAll.Tests.Integration.AgentFramework.ProjectStructureAgentToolIntegrityEndToEndTests.
- Filter: FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ProjectStructureAgentToolIntegrityEndToEndTests
- Expected: 4 cases: malformed/future prose no node and failed status; corrected call/one visible canonical node; next-turn scoped failure; unrelated target success does not resolve.
- Owning solution: tests/Solutions/CanDoItAll.Tests.Integration.slnx.
- Real Web/runtime/persistence/project services; fake only the external model boundary.
- Live matrix has four separately recorded cases: direct positive, direct correction opportunity, shared positive, shared correction opportunity. Use the same installed Ollama model. Each records run/profile/model/route, calls, safe results, receipt, canonical graph/file and visible state. The correction opportunity passes on correction or honest failure; a false success fails.

## Browser proof

SB05/SB06 use Playwright MCP at 2048×1100 CSS pixels. Preserve the existing canvas, contextual chat and runtime-details surfaces. Capture normal and open runtime-details states. Assert without page refresh: terminal status, matching new node visibility and parent, canonical content; inspect first viewport, canvas versus overlay scrolling, clipping and error/status presentation. No mobile scope.

## Static and broad gates

Every protected source/test change follows docs/testing.md portability-static: scanner self-tests, complete scan including untracked protected files, review all ADDED/STALE findings, repair defects, refresh only intentional baseline deltas, and final enforcement without --write-baseline.

At the final frozen SB06 checkpoint, run the documented broad stable gate once because root package versions, ToolExecutionReceiptRecord/public execution persistence, and composition are cross-cutting invalidation triggers. If the actual final diff avoids all of those triggers, update this plan with diff evidence before waiving it; do not quietly omit the gate.

Run tools/Validation/Test-Documentation.ps1 after maintained documentation changes and validate this bundle at prepared/completed stages as appropriate.

## Semantic evidence

Governed SB01 and SB03 require manifests and semantic invariant contracts only when executed. Each completed invariant records: raw note, expected behavior, rejected shallow implementation, failing-first and passing tests, changed production sources, production assertions, adversarial case and downstream dependency check. Evidence uses repo:// or bundle:// paths plus SHA-256 hashes. Preparation must not fabricate those artifacts.
