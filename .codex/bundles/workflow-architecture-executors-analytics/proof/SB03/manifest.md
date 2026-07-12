# SB03 Proof Manifest

- Subbundle ID: SB03
- Status: Completed
- Baseline commit: 5f9d13dc04362442073b4782d544fbb88429af55
- Owned requirements: WF-EXEC-01, WF-EXEC-02, WF-EXEC-03, WF-EXEC-04, WF-PLUGIN-01
- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.md

## Evidence

- Failing-first: bundle://proof/SB03/failing-document-image.txt
- Failing-first: bundle://proof/SB03/failing-storage-spreadsheet.txt
- Failing-first: bundle://proof/SB03/failing-command-safety-blocker.txt
- Structural red gate: bundle://proof/SB03/failing-plugin-testability.txt
- Passing: bundle://proof/SB03/passing-build.txt
- Passing: bundle://proof/SB03/passing-standard-executors.txt
- Passing: bundle://proof/SB03/passing-storage-spreadsheet.txt
- Passing: bundle://proof/SB03/passing-command-safety-blocker.txt
- Passing: bundle://proof/SB03/passing-plugin-executors.txt
- Passing integration: bundle://proof/SB03/passing-plugin-integration.txt
- Anti-stub: bundle://proof/SB03/anti-stub.txt
- Architecture source/dependency proof: bundle://proof/SB03/architecture-snapshot.txt

## Named Test Proof

- Test name: Document_executor_prefers_explicit_source_and_delegates_all_settings
- Test name: Document_executor_propagates_cancellation_to_shared_operation
- Test name: Image_inspect_executor_delegates_to_shared_image_operation
- Test name: Image_analyze_executor_returns_typed_payload_and_known_usage
- Test name: Image_analyze_executor_preserves_tokens_when_price_is_unknown
- Test name: Storage_list_directory_returns_only_direct_files_and_directories_and_applies_filters
- Test name: Spreadsheet_preview_resolves_once_and_delegates_once_to_shared_service
- Test name: PreviewWorkbookReturnsBoundedTypedPreviewAndPreservesFormulas
- Test name: CommandProcessRemainsPlannedWithActionableSafetyBlockers
- Test name: BundledPluginContributionsMatchManifestDefaultsSchemaAndSimulation
- Test name: GmailConcreteExecutorsExecuteThroughFakeWorkflowPorts
- Test name: Office365ConcreteExecutorsExecuteThroughFakeWorkflowPorts
- Test name: DockerConcreteExecutorsExecuteOnlyTypedRecipesThroughFakeHostPort
- Test name: RuntimePackageContributionRejectsManifestRuntimeMetadataDrift

## Changed-File SHA-256

| File | SHA-256 |
|---|---|
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs | 84b70fe7a3c53d39e28b29ecd3f1fe953ce4dec115083a4a7e3c401c9d66c95f |
| repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/BuiltInWorkflowExecutorDescriptors.cs | def2ae54e60686c16355eaf4a49d812455301a84951f58021547f57a70a5a2b2 |
| repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowInputJsonStringResolver.cs | e8cbc269c7065b584b5b13c20094a96cb3db2491193a59a5cb2fdf1fd09c2456 |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/DocumentToMarkdownWorkflowExecutor.cs | 0c63c04f866bf12899f90fc04d49b36952b8e6d875a83c96e892632f7f38282f |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/SpreadsheetWorkflowExecutor.cs | e44078ea95fd3bee16120190371ecca4348d2103dae017ef7eda45545114b645 |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/ImageInspectWorkflowExecutor.cs | 00133afbc0a11386a9073f0f4e9e2d48468dfc899f3691df0dc1c753eedda559 |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/ImageAnalyzeWorkflowExecutor.cs | 14c19ca0dcdd0882bc9e59afa1b3cabe3584425297c07edfa9056c8a534c1ccd |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkspaceFileWorkflowExecutor.cs | c99b478e3a979beda349f3f56f037a55c8eab63c3e4273046b28ec9ef1ae02c6 |
| repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Spreadsheets/SpreadsheetDocumentModels.cs | f4361e47b94a1c5dde1808e8e39858a2a1b906d1426207180262e9f45a43c644 |
| repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Spreadsheets/ClosedXmlSpreadsheetDocumentService.cs | 497ce2318f485080588ba512ab40a8b9ecd516e81417e66c8a4e53e95c5acb78 |
| repo://src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs | 0a989b6118848c617a74deac64eaf792ac744947c518fe1b491f68a7a0977fa8 |
| repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/PluginWorkflowExecutorRuntimeRegistration.cs | 02bff7018497ecab7ab2c63d70a4555cffd895939f6ef937508f44c702075a0b |
| repo://src/plugins/Implementations/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs | ebd324faa3b7687ffc995c41bfc4f3f33de1bbca66535e31af7e80e001add401 |
| repo://src/plugins/Implementations/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs | e261f85319f43ab28f09d81f51cb8785e09fe5c4a4d699438e2b8c964cb8b4c9 |
| repo://src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs | 3b0f2c46c992d47a7628f5d3d6058bd13a03a8041b3ad3f32dc671387d71b996 |

## Result

- SB03-STANDARD-ADAPTERS is satisfied by three runnable shared-operation nodes, real-DI contribution proof, usage/cancellation behavior, and source anti-duplication audit.
- SB03-BOUNDED-FILE-PREVIEW is satisfied by appended compatible operations, exact delegation, bounds/failure tests, and shared-service ownership.
- SB03-COMMAND-SAFETY is satisfied by an explicit tested blocker; no unsafe command executor is exposed.
- SB03-PLUGIN-PARITY is satisfied by narrow production ports, all-nine direct execution/parity tests, manifest JSON validation, and runtime-package metadata drift rejection.
