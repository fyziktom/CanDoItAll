# SB02 Semantic Invariants

## Content conversion boundary

- Invariant ID: SB02-CONVERSION-BOUNDARY
- Source raw note: use one key document/MarkItDown implementation for runtime tools and workflow executors where reasonable.
- Expected behavior: the inward contract returns Markdown content and truncation metadata; ManagedCode.MarkItDown converts only; workspace artifact orchestration owns path policy, atomic writes, previews, and mutation receipts.
- Disallowed shallow implementation: keep OutputPath in the converter, let the SDK adapter write artifacts, or duplicate conversion inside an executor.
- Failing-first test: conversion contract and source architecture assertions failed 17 of 18 checks before implementation in bundle://proof/SB02/failing-first.txt.
- Passing test: real HTML, PDF, DOCX, and XLSX content plus bounded output, cancellation, missing input, atomic overwrite, write failure, preview, and receipt assertions pass in bundle://proof/SB02/passing-shared-operations.txt.
- Changed source files: repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceDocumentMarkdownConverter.cs, repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Markdown/ManagedCodeMarkItDownDocumentMarkdownConverter.cs, and repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs.
- Production assertions: Hosting and Modules.AgentFramework register the ManagedCode adapter at the correct workspace lifetime; the full solution compiles these consumers in bundle://proof/SB02/passing-build.txt.
- Red-team negative case: negative limits throw, cancellation propagates, converter failure writes nothing, incomplete success is rejected, and a blocked output preserves existing content with a non-mutating failure receipt.
- Downstream dependency check: SB03 can implement document.to-markdown without depending on a MAF runtime tool or copying SDK behavior; dependency evidence is bundle://proof/SB02/architecture-snapshot.txt.

## Source-ingestion delegation and ownership

- Invariant ID: SB02-SOURCE-INGESTION-DELEGATION
- Source raw note: improve workflow testability/flexibility and eliminate separate file/MarkItDown implementations.
- Expected behavior: one non-partial executor orchestrates candidate collection, file resolution, and content reading; PDF, DOCX, HTML/HTM, and XLSX delegate to the shared converter; XLS and ZIP keep explicit bounded legacy/manifest paths.
- Disallowed shallow implementation: rename partial fragments, retain direct PDF/DOCX/HTML/XLSX parsing, silently read raw content after conversion failure, or probe files from the pure candidate collector.
- Failing-first test: partial shape, direct parser markers, PdfPig, missing collaborators, and missing converter dependency failed in bundle://proof/SB02/failing-first.txt.
- Passing test: format routing, direct collaborator rules, path policy, deduplication, exact remaining budget, contract-violation rejection, no raw fallback, ZIP, XLS, and cancellation assertions pass in bundle://proof/SB02/passing-shared-operations.txt.
- Changed source files: repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/SourceIngestionWorkflowExecutor.cs, repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceCandidateCollector.cs, repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceFileResolver.cs, and repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceDocumentReader.cs.
- Production assertions: source-ingestion output JSON, limits, explicit sourceErrors, absolute-path opt-in, deduplication, text/ZIP behavior, and HTTP-download handoff remain exercised by real executor tests.
- Red-team negative case: a converter that returns oversized Markdown, impossible totals, or inconsistent truncation is rejected explicitly rather than repaired; disallowed absolute paths and extensions fail.
- Downstream dependency check: SB03 can add conversion nodes without reopening ingestion and project/scheduler workflows retain bounded source behavior.

## Typed file and image operations

- Invariant ID: SB02-TYPED-SHARED-OPERATIONS
- Source raw note: tools and workflow executors should reuse file and image key functions instead of parallel implementations.
- Expected behavior: workspace file results share a compile-time success/message contract; storage.file uses no reflection; image path policy, byte limits, receipts, and PNG/JPEG/GIF parsing live in one image operation consumed by the runtime tool adapter and future workflow adapters.
- Disallowed shallow implementation: inspect result properties by name, leave image parsing in WorkspaceArtifactToolService, or add a second executor-specific image reader.
- Failing-first test: the failing architecture gate established the missing shared-operation pattern; focused negative/source tests now prevent reflection and duplicate image ownership.
- Passing test: typed exact-message failures, eight result-contract assertions, image metadata/bytes/limits/path denials, adapter delegation, DI lifetime, and source-boundary tests pass in bundle://proof/SB02/passing-shared-operations.txt.
- Changed source files: repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workspace/WorkspaceFileToolModels.cs, repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkspaceFileWorkflowExecutor.cs, repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceImageOperationService.cs, and repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs.
- Production assertions: serialized positional result shapes are unchanged; Hosting uses a singleton workspace operation, Modules.AgentFramework uses a scoped workspace operation, and standalone MAF composition receives the same service.
- Red-team negative case: path escape and byte-limit inputs fail before returning bytes; source audits reject reflection and image parsing in the transport adapter.
- Downstream dependency check: SB03 image.inspect/image.analyze and existing storage.file share the same operation implementations; project-reference proof is bundle://proof/SB02/architecture-snapshot.txt.

## Provider image-analysis seam

- Invariant ID: SB02-IMAGE-ANALYSIS-SEAM
- Source raw note: image tools should become workflow executors with token-aware analytics through one reasonable implementation.
- Expected behavior: SDK-free Core request/result contracts feed one Maf provider-gateway adapter; the workspace runtime plugin delegates single/multi image analysis while retaining access checks, prompt/model selection, deterministic evidence, and token counts.
- Disallowed shallow implementation: copy provider calls into the workflow executor, keep ProviderChatAttachment construction in the runtime tool plugin, locate services inside the plugin/composer, swallow cancellation/provider failures, or return an unavailable service as success.
- Failing-first test: the initial SB02 architecture lacked a reusable analysis service; focused adversarial tests now reject unsupported vision, empty/invalid sources, access violations, service location, and unavailable behavior.
- Passing test: request mapping, token propagation, validation, cancellation/failure propagation, DI/standalone composition, and single/multi runtime-plugin regression pass in bundle://proof/SB02/passing-image-analysis.txt.
- Changed source files: repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ImageAnalysis/AgentImageAnalysisContracts.cs, repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageAnalysisService.cs, and repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs.
- Production assertions: the real provider gateway remains the sole provider-call implementation; runtime dependencies carry the registered or explicitly composed real adapter; token counts remain in existing workspace results.
- Red-team negative case: disabled/non-vision providers, invalid sources, cancellation, and provider exceptions never become fabricated successful analysis; the plugin contains no gateway or service-location reference.
- Downstream dependency check: SB03 can add image analysis as a thin adapter and SB05 can consume canonical input/output token observations from its result.
