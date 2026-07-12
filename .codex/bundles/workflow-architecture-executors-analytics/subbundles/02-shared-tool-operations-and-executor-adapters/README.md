# Shared Tool Operations And Executor Adapters

## Status

- `Completed`

## Objective

- Make tools and executors share typed document/file/spreadsheet/image operations and replace source-ingestion parsing duplication with cohesive collaborators.

## Success Criteria

- ManagedCode.MarkItDown is the one document-conversion implementation used by artifact tools and source ingestion.
- `SourceIngestionWorkflowExecutor` is not partial and orchestrates separately testable candidate, path, and content services.
- Shared operations are independent of workflow-node and agent-tool transports.
- Existing extraction limits, diagnostics, supported inputs, and explicit failures are characterized and preserved or intentionally documented.

## Covered Inputs

- WF-ARCH-03, WF-OPS-01, and the request for one implementation reused by tools/executors.
- Duplicate PDF/DOCX/HTML/XLS/XLSX parsing and embedded image orchestration findings.

## Prerequisites

- SB01 progression gate passes.
- Existing converter/artifact/source-ingestion tests are identified and characterization fixtures exist.

## Exact Source References

- `repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Markdown/ManagedCodeMarkItDownDocumentMarkdownConverter.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs`
- `repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/SourceIngestionWorkflowExecutor.cs`
- `repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceDocumentReader.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceImageOperationService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ImageAnalysis/AgentImageAnalysisContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageAnalysisService.cs`
- `repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkspaceFileWorkflowExecutor.cs`
- `repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/SpreadsheetWorkflowExecutor.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowExecutorCategoryIsolationTests.cs`

## Deliverables

- SDK-free content conversion contract/result and ManagedCode adapter implementation.
- Artifact-tool and source-ingestion adapters over the same conversion service.
- Cohesive source candidate/path/reader collaborators with direct unit tests; no partial executor.
- Typed shared seams for spreadsheet preview and image inspection/analysis required by SB03.
- Removal of reflective workflow-file result interpretation if the touched result contracts allow explicit typed handling.

## Dependency Impact

- SB03 cannot add document/image nodes before shared operations are proven.
- SB05 relies on image analysis returning canonical usage observations.
- Tool behavior regressions here affect agent runtime as well as workflows.

## Validation Depth

- `Critical foundation` with semantic parity fixtures for representative HTML/PDF/DOCX/XLSX/text input and negative path/format cases.

## C# Architecture Impact

- Converts a partial executor god-object into owned services and establishes ports/adapters for shared behavior.

## Boundary Ownership

- Common/operation contracts own content results; Tools.Documents owns ManagedCode adapter.
- Runtime tools own grants/receipts; executors own workflow mapping/audit; neither calls the other.

## Dependency Direction

- Adapters depend inward on operation contracts. Shared operations do not reference MAF tool attributes, workflow nodes, Blazor, or plugin hosts.

## Pattern Decision

- Use PSR-02 Ports And Adapters. Reject executor-to-tool invocation and duplicate parsing.

## Testability Contract

- Fake converter/operation services must prove exact delegation and error mapping.
- Golden/semantic fixtures prove output content, truncation, diagnostics, and unsupported-format behavior.

## Partial Class Policy

- `SourceIngestionWorkflowExecutor` must end as one small non-partial orchestrator. Candidate/path/reader services are separate sealed types, not renamed partial fragments.

## Architecture Proof Required

- No-partial/source-parser anti-stub audit, direct collaborator tests, and project dependency proof that ManagedCode remains an outer adapter.

## Implementation Steps

1. Add characterization fixtures/tests for current source ingestion and converter behavior.
2. Introduce content-returning conversion contract and implement it in Tools.Documents.
3. Adapt artifact conversion without losing output path, overwrite, preview, or receipt semantics.
4. Extract candidate/path/content responsibilities and delegate source ingestion to shared operations.
5. Extract only the spreadsheet/image seams required by known consumers.
6. Remove duplicate parsers and replace shallow partial/file-size tests.

## Scope Exceptions

- ZIP manifest behavior may remain a source-ingestion-specific reader because it is not document-to-Markdown conversion; isolate it explicitly.

## Do Not Do

- Do not place ManagedCode SDK types in inward contracts.
- Do not make workflow executors depend on agent runtime plugins.
- Do not silently change supported extensions or return empty Markdown on conversion failure.

## Acceptance Checklist

- One document conversion implementation.
- No partial source-ingestion executor and no duplicate PDF/DOCX/XLSX parser path.
- Direct fake-service and fixture tests pass.
- Artifact tools preserve access/receipt semantics.
- Affected projects build without new cycles.

## Proof Required

- Failing-first characterization or duplication guard transcript.
- Passing semantic fixtures, direct delegation tests, and negative unsupported/path tests.
- Anti-stub search proving removed parser code and no tool invocation from executors.
- `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md` during execution.

## Browser Validation Logging

- `N/A: operation behavior is proven below UI in SB02.`

## Progression Gate

- Passed. Shared conversion and image operations, provider analysis, no-partial ownership, typed file results, runtime-tool regression, the full build, and dependency proof are recorded in `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB02 only. Characterize behavior first, make ManagedCode.MarkItDown the single document conversion adapter, replace source-ingestion partial fragments with cohesive collaborators, and prove both runtime-tool and workflow adapters delegate to the same typed operations.
```
