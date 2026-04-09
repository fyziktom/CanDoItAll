# PRM-F12 — Import/export, templates, Mermaid, and prompt-flow seeding

## Objective

Allow process definitions to be imported from Mermaid and exported back to Mermaid/JSON packages, and seed starter templates from existing prompt-flow patterns where it fits.

## Priority and wave

- Priority: **Medium**
- Planned wave: **Wave 2**
- Depends on: **PRM-F02, PRM-F09**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Mermaid mindmap and flowchart can be imported into a draft process with explicit limitations recorded.
- Published processes can be exported as Mermaid and JSON packages.
- Starter templates can reference prompt-flow patterns without making Prompt Factory the canonical process store.
- Import warnings are explicit whenever semantics do not round-trip perfectly.

## Non-goals

- Do not claim Mermaid round-tripping is lossless when semantics are richer than Mermaid can carry.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessImportExportService.cs (new)`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs (reference pattern)`
- `output/prompt-library/factory-prompt-flow-templates.seed.json`
- `tests/CanDoItAll.Tests.Integration/ProcessImportExportIntegrationTests.cs (new)`
