# SB08 Proof Manifest

## Status

- Partial implementation proof captured.
- Focused build, focused MAF unit tests, handoff integration smoke, source assertions, changed-file hashes, and final CodeAnalytics proof are captured.
- Bundle final closure remains `Pass with follow-up required` because `RuntimeCapabilityComposer` and `MafAgentRuntime` remain large hotspots and the full unit project has unrelated failures.

## Captured Artifacts

| Artifact | Path | Result |
| --- | --- | --- |
| Final build | `bundle://proof/SB08/transcripts/final-build.txt` | Passed: MAF project build, 0 warnings, 0 errors. |
| Final focused unit tests | `bundle://proof/SB08/transcripts/final-focused-unit-tests.txt` | Passed: 56/56 MAF architecture/composition/image-model tests. |
| Final integration smoke | `bundle://proof/SB08/transcripts/final-integration.txt` | Passed: 3/3 `MafAgentRuntimeHandoffTests`. |
| Full unit project | `bundle://proof/SB08/transcripts/full-unit-tests.txt` | Failed outside this slice: 13 failed, 1791 passed. |
| Final CodeAnalytics | `bundle://proof/SB08/transcripts/final-codeanalytics.txt` | Snapshot `snap-20260706191451-275f822a`; cycles `[]`; residual hotspots recorded. |
| Source assertions | `bundle://proof/SB08/transcripts/source-assertions.txt` | No runtime partial declarations; moved responsibilities live in extracted owners. |
| Changed-file hashes | `bundle://proof/SB08/changed-file-hashes.txt` | SHA256 hashes captured for changed production/test files. |
| Architecture gate | `bundle://reviews/csharp-architecture-gate.md` | Pass with follow-up required. |

## Implemented Isolation Slices

| Responsibility | Old owner | New owner | Direct proof |
| --- | --- | --- | --- |
| Pending tool approval cache, mapping, and rehydration | `MafAgentRuntime` | `MafApprovalContinuationDriver` | `MafRuntimeArchitectureServicesTests.MafApprovalContinuationDriver_*` |
| Runtime session serialization skip/scrub/timeout policy | `MafAgentRuntime` | `MafRuntimeSessionPersistenceDriver` | `MafRuntimeArchitectureServicesTests.MafRuntimeSessionPersistenceDriver_*` |
| Response assembly and usage diagnostics helpers | `MafAgentRuntime` | `MafRuntimeResponseAssembler` | focused unit regression and source assertions |
| Script side-effect policy inspection | `MafRuntimeAgentFactory` | `MafScriptPolicyInspectionService` | focused MAF build and source assertions |
| Capability access planning and policy construction | `RuntimeCapabilityComposer` partial cluster | `RuntimeCapabilityAccessPlanner`, `RuntimeCapabilityAccessPolicyBuilder`, `RuntimeToolProcessIntentPolicy`, `RuntimeStorageToolNames` | `MafAgentRuntimeToolProviderCompositionTests`; source assertion blocks composer partials |
| Runtime/catalog descriptor mapping | `RuntimeCapabilityComposer` partial cluster | `RuntimeCapabilityDescriptorCatalog`, `RuntimeConfiguredWorkspaceToolDescriptorCatalog` | `RuntimeCapabilityDescriptorCatalog_creates_tool_descriptor_without_composer` |
| Registered runtime tool-provider attachment | `RuntimeCapabilityComposer` partial cluster | `RuntimeRegisteredToolProviderAttacher` | `MafAgentRuntimeToolProviderCompositionTests` |
| Configured workspace tool-set creation | `ToolCapabilityBuilder` partial | `ConfiguredWorkspaceToolSet` | no `partial class ToolCapabilityBuilder`; focused composition tests |
| Workspace image-analysis model selection | `WorkspaceRuntimePlugin` | `WorkspaceImageAnalysisModelResolver` | `MafAgentRuntimeImageAnalysisModelTests`; source assertion blocks regression |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Approval continuation records | `MafApprovalContinuationDriver` | `MafAgentRuntime.RespondToPendingApprovalsAsync` and run persistence | Pending approval request through continuation response | Direct positive/rehydration unit tests and runtime source assertion. |
| Runtime session compatibility state | `MafRuntimeSessionPersistenceDriver` | `MafAgentRuntime` response assembly | After provider execution/session serialization | Direct skip-policy unit test and runtime source assertion. |
| Capability access plan | `RuntimeCapabilityAccessPlanner` | `RuntimeCapabilityComposer` and runtime tool composition | Capability composition before tool/skill/MCP attachment | Tool-provider composition regression tests and no composer partial declarations. |
| Workspace image-analysis model | `WorkspaceImageAnalysisModelResolver` | `WorkspaceRuntimePlugin`, `InputAttachmentPreparer`, `InputAttachmentSupport` | Image analysis and request-scoped attachment model selection | Direct resolver tests and source assertion that plugin no longer owns local implementation. |
| Final architecture proof | SB08 | Maintainers/future bundles | Bundle closure | Gate records residual hotspots and blocks full closure claim. |

## Closure Criteria

- Focused proof is artifact-backed: satisfied.
- Raw notes are partially closed: partial-class hiding and several responsibility slices are fixed; thin-runtime target is not fully closed.
- Follow-up risks are concrete: `RuntimeCapabilityComposer`, `MafAgentRuntime`, `WorkspaceRuntimePlugin`, `McpCapabilityBuilder`, and full-suite unrelated failures are recorded in the final gate/report.
