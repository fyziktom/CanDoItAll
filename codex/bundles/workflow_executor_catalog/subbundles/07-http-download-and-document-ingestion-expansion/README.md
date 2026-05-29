# 07-http-download-and-document-ingestion-expansion

## Status

- Status: `Completed`

## Closure Notes

- Expanded `http.fetch` with workspace download output, guarded secret headers, and default SSRF/private-network blocking.
- Expanded source ingestion to consume downloaded output paths and document/archive inputs through workspace-scoped file access.
- Added tests for safe HTTP download, private network blocking, and source ingestion from prior executor output.
- Proof manifest: `bundle://proof/SB07/manifest.md`
- Semantic invariants: `bundle://proof/SB07/semantic-invariants.md`

## Objective

Improve network and document workflows while preserving SSRF, workspace, and artifact safety.

## Covered Inputs

- RN02: Users need HTTP/document helper workflows.
- RN03: Folder/file workflows should ingest downloaded or local document content safely.
- R4: Source ingestion must support folder and document scenarios clearly.
- R9: HTTP workflows must support safe download-to-workspace and content artifacts.
- R11: Scenario harness must cover document ingestion where practical.

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed for artifact content behavior.
- SB03 closure gate passed for workspace file output.
- SB05 closure gate passed if downloaded content is rendered in reports.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/HttpFetchWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/SourceIngestionWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Scope

- Refactor HTTP executor to use `IHttpClientFactory` where compatible with tests.
- Add SSRF/allowlist policy that blocks loopback, link-local, and private network targets by default.
- Add bounded download-to-workspace operation with content type, filename, overwrite, and byte limits.
- Register downloaded content artifacts through SB02 boundaries.
- Expand source ingestion for DOCX, HTML, CSV, ZIP manifest/list, and PDF metadata/page counts where dependencies already exist or can be added safely.

## Dependency Impact

- SB09 templates can include HTTP download and document extraction only after this phase proves safety.
- SB10 scenario harness depends on safe download or documents being clearly scoped.

## Validation Depth

- Unit tests with fake HTTP handler for success, max bytes, content type, filename policy, download write, and artifact registration.
- Negative tests for loopback/private/link-local targets and disallowed schemes.
- Ingestion tests for each implemented document type with truncation and status summaries.

## Implementation Steps

1. Audit current HTTP executor construction and tests.
2. Introduce a typed network safety policy with default-deny private target behavior.
3. Add download-to-workspace settings and implementation through workspace services.
4. Add document ingestion extractors only for feasible, bounded formats.
5. Add descriptors, schema metadata, and targeted tests.

## Do Not Do

- Do not allow SSRF-sensitive targets by default.
- Do not store downloaded content outside workspace scope.
- Do not add live network tests when fake handlers can prove behavior.
- Do not claim full document conversion for formats that only have metadata or text extraction.

## Acceptance Checklist

- HTTP fetch/download does not create an SSRF footgun.
- Downloaded content can feed workspace file or artifact workflows.
- Document extraction returns clear status values and truncation summaries.
- Network and workspace safety failures are actionable.

## Proof Required

- Passing HTTP/download and source-ingestion targeted test transcripts.
- Negative proof for SSRF and unsafe filenames/paths.
- Changed-file hashes, source assertions, and anti-stub audit.
- Execution report row for SB07 closure.

## Browser Validation Logging

- N/A unless download or ingestion UI changes; if UI changes, record route, viewport, actions, screenshots, and result.

## Progression Gate

- Continue to SB08 only after HTTP download and document ingestion surfaces are safe, bounded, and honest about implemented formats.

## Suggested Agent Prompt

Use SB07 to harden HTTP and document ingestion. Prefer fake HTTP tests, enforce default SSRF guardrails, and write downloads only through workspace and artifact boundaries.
