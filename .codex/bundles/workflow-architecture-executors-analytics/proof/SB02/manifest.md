# SB02 Proof Manifest

- Subbundle ID: SB02
- Status: Completed
- Baseline commit: 5f9d13dc04362442073b4782d544fbb88429af55
- Owned requirements: WF-ARCH-03, WF-OPS-01
- Semantic invariant contract: bundle://proof/SB02/semantic-invariants.md

## Evidence

- Failing-first transcript: bundle://proof/SB02/transcripts/closure.txt
- Passing transcript: bundle://proof/SB02/transcripts/closure.txt
- Anti-stub transcript: bundle://proof/SB02/transcripts/closure.txt
- Failing-first: bundle://proof/SB02/failing-first.txt
- Passing: bundle://proof/SB02/passing-build.txt
- Passing: bundle://proof/SB02/passing-shared-operations.txt
- Semantic positive proof: bundle://proof/SB02/passing-image-analysis.txt
- Anti-stub: bundle://proof/SB02/anti-stub.txt
- Architecture source/dependency proof: bundle://proof/SB02/architecture-snapshot.txt

## Named Test Proof

- Test name: ConvertToMarkdownAsync_extracts_recognizable_pdf_content
- Test name: ConvertToMarkdownAsync_extracts_recognizable_docx_content
- Test name: ConvertToMarkdownAsync_extracts_recognizable_xlsx_content
- Test name: ConvertDocumentToMarkdown_overwrites_existing_output_with_full_markdown
- Test name: DocumentExtensionsDelegateToSharedConverter
- Test name: DocumentReaderRejectsConverterContractViolations
- Test name: ConversionFailureBecomesSourceErrorWithoutRawFallback
- Test name: WorkspaceFileExecutor_DoesNotInspectResultsWithReflection
- Test name: ArtifactService_ImageMethodsDelegateToInjectedOperationService
- Test name: AnalyzeAsync_maps_gateway_request_and_preserves_token_usage
- Test name: AnalyzeAsync_rejects_provider_model_without_vision_without_calling_gateway
- Test name: AnalyzeImageFile_delegates_to_analysis_service_and_preserves_result_shape
- Test name: Workspace_plugin_and_composer_do_not_locate_or_fallback_image_analysis_services

## Changed-File SHA-256

| File | SHA-256 |
|---|---|
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceDocumentMarkdownConverter.cs | fe4d2ca2f738570facc2c80cdae67828c36d71aecf4c283ca1c7d078a436bedc |
| repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Markdown/ManagedCodeMarkItDownDocumentMarkdownConverter.cs | 1616f57ae3d6f904d1ca5dfdd57ee1cfe0ebb1ab14d8e10de1f56cc177718cec |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs | 5264987197306d8ff772c6912c57bd9fbbc6f1a4493b33773ddcaede5f3009b0 |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceImageOperationService.cs | b9a2702e0a06113e9946931c5cac4ff0c685190425cefb04327d7607e291031c |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ImageAnalysis/AgentImageAnalysisContracts.cs | 5ea3cddbf8816e059dcbbd8abc21857bc65c9d7fef144b0cf3cff805d9c5cbce |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageAnalysisService.cs | c7e06f0613d7c736251e1c257204a7bed0e1503c00685a0088585165dc4d32e8 |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs | cecb618fcf9d12e9656bc22f143f5ab268dfbbb40b5ac82862a15aaead398932 |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/SourceIngestionWorkflowExecutor.cs | ef3813e9ded8e7d75f4598d77a795b0d7c86ab96834a9966994745239d349520 |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceCandidateCollector.cs | dc22e30495b32900fa96386a089e04bbbdaf76e70f830a401b47031aef890fb8 |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceFileResolver.cs | 5855be1f0aa313354d791ce8be341c4927e5e89d172dbc77b2b6d44ea87a5dce |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceDocumentReader.cs | eb20e4423e9c4d2e307820145db2507cc477674ff6a6a2fb2f2373613deec3ac |
| repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workspace/WorkspaceFileToolModels.cs | 523c81155549104374e6d0008a95307c55f83e3b568155941609ccd0ea42acec |
| repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkspaceFileWorkflowExecutor.cs | 407e87a06861b36dd16fcef0680f97c43b43eb384cd83aeec5d8df6074bfbf42 |

## Result

- SB02-CONVERSION-BOUNDARY is satisfied by real format fixtures, conversion-only source, atomic artifact tests, and full build proof.
- SB02-SOURCE-INGESTION-DELEGATION is satisfied by no-partial/direct-collaborator tests, exact delegation, explicit failure mapping, and parser-removal audit.
- SB02-TYPED-SHARED-OPERATIONS is satisfied by typed file results, extracted image operations, lifetime/delegation tests, and source audits.
- SB02-IMAGE-ANALYSIS-SEAM is satisfied by the single gateway adapter, preserved runtime-tool behavior/tokens, adversarial validation, and no-service-location audit.
