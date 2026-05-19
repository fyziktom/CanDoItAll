# 03-file-analysis-workflow-examples

## Status

- `Completed`

## Objective

- Add file-analysis workflow examples for generating Mermaid graphs from input files and summarizing source-code files, then run final validation and closure proof.

## Success Criteria

- New Mermaid and source-code workflow keys load from `file-analysis-workflows.yaml`.
- Each workflow uses `source.ingest`, an LLM call, and project-structure `CreateAsset`.
- Source ingestion settings include common text and code extensions.
- Targeted tests and bundle validators pass.

## Covered Inputs

- R6, R7, R8.
- Raw notes `N003`, `N004`, and final closure for `N005`.

## Prerequisites

- `01-template-pack-file-loading-foundation` closure gate passed or manifest file loading otherwise proven.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Workflows\workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\SourceIngestionWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ProjectStructureWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs`

## Deliverables

- `file-to-mermaid-graph-asset` template.
- `source-code-file-summary-to-project` template.
- New file to create: `C:\repositories\CanDoItAll\Templates\Workflows\workflows\file-analysis-workflows.yaml`.
- Tests proving keys load, graphs compile, and core executor nodes exist.
- Final bundle execution report and closure validator updates.

## Dependency Impact

- This is the final implementation phase. Weak proof here would leave requested non-email examples incomplete.

## Validation Depth

- Template and final closure validation.

## Implementation Steps

1. Create `file-analysis-workflows.yaml`.
2. Add Mermaid graph template that ingests input sources and stores Markdown with Mermaid fenced code blocks.
3. Add source-code summary template that ingests code files and stores a concise Markdown review.
4. Extend targeted tests for both keys.
5. Run targeted tests and bundle validators.
6. Update execution report and raw-note closure table.

## Scope Exceptions

- Live project-structure mutation proof is limited to graph/settings validation unless a configured runtime project is available.

## Do Not Do

- Do not add custom Mermaid rendering code.
- Do not add source-code parsers; this template uses source ingestion plus LLM summarization.
- Do not hard-code workflow graphs in C#.

## Acceptance Checklist

- Mermaid workflow key loads and graph compiles.
- Source-code summary workflow key loads and graph compiles.
- Both workflows include `source.ingest` and `project-structure` executor nodes.
- Targeted tests pass.
- Completed-stage bundle validator passes or any failure is recorded.

## Proof Required

- Targeted unit test command.
- `validate_bundle.py --stage completed`.
- Execution report row updated.

## Browser Validation Logging

- N/A - no browser-visible UI changes.

## Progression Gate

- Final closure may proceed only after both file-analysis templates load, graph construction succeeds, tests pass, and raw-note closure is updated.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
