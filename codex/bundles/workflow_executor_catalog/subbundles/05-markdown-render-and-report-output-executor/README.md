# 05-markdown-render-and-report-output-executor

## Status

- Status: `Completed`

## Closure Notes

- Implemented `markdown.render` for deterministic template rendering, table bindings, and workspace file output.
- Runtime backend now records configured file artifacts for markdown and other file-producing executor outputs.
- Added tests for markdown table rendering, output file persistence, and catalog metadata.
- Proof manifest: `bundle://proof/SB05/manifest.md`
- Semantic invariants: `bundle://proof/SB05/semantic-invariants.md`

## Objective

Implement the planned `markdown.render` executor for reports and user-facing workflow outputs.

## Covered Inputs

- RN02: Users need report output without custom code.
- RN03: Local folder workflows need Markdown summaries.
- R2: Report artifacts must be retrievable through the artifact content boundary.
- R6: Implement Markdown/report rendering executor with file output and artifact integration.
- R11: Scenario harness must cover Markdown output.

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed for artifact content truth.
- SB03 closure gate passed for workspace file output.
- SB04 closure gate passed for structured JSON inputs.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkflowExecutorJson.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkspaceFileWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://Templates/Workflows/manifest.yaml`

## Scope

- Add typed settings for inline template, template workspace path, JSON bindings, table rendering, evidence table rendering, output workspace path, and append/overwrite.
- Implement safe placeholder replacement and JSON-array-to-Markdown-table rendering.
- Support writing output to a workspace file.
- Integrate report output with the artifact content boundary from SB02.
- Add tests for missing placeholders according to explicit settings.

## Dependency Impact

- SB07 downloads and SB09 examples can produce user-facing reports.
- SB10 scenario harness depends on report output file and artifact retrieval.

## Validation Depth

- Unit tests for Markdown from JSON payloads, array tables, output file writes, artifact content retrieval, append/overwrite, and missing placeholder behavior.
- Negative tests for path escape through template or output path.
- Artifact proof must cite SB02 content store behavior.

## Implementation Steps

1. Define settings and result models.
2. Implement placeholder binding and table rendering with explicit escaping rules.
3. Read template files and write output files through workspace services.
4. Register content-bearing artifacts where report output is captured.
5. Add targeted tests and descriptor schema metadata.

## Do Not Do

- Do not add arbitrary template code execution.
- Do not bypass workspace path policy for template or output paths.
- Do not imply an artifact exists unless retrievable content is written.
- Do not silently drop missing placeholders unless settings explicitly request tolerant mode.

## Acceptance Checklist

- Users can produce Markdown reports from structured workflow data without custom code.
- Markdown tables render deterministic columns and values.
- Report output can be saved as a workspace file.
- Report artifact content is retrievable when an artifact is created.

## Proof Required

- Passing targeted Markdown executor transcript.
- Negative proof for missing placeholder or path escape behavior.
- Changed-file hashes, source assertions, anti-stub audit, and artifact retrieval proof.
- Execution report row for SB05 closure.

## Browser Validation Logging

- N/A unless report links or authoring UI change in this phase; if they do, record browser route, viewport, assertions, screenshots, and result.

## Progression Gate

- Continue to SB06 only after Markdown output can be produced from JSON and saved/retrieved through workspace/artifact paths.

## Suggested Agent Prompt

Use SB05 to implement a safe Markdown report executor. Keep templating deterministic, integrate file/artifact output through existing boundaries, and prove the report content can be retrieved.
