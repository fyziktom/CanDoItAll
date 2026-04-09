# Codex task — PRM-F12

Implement **Import/export, templates, Mermaid, and prompt-flow seeding** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable agent registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Mermaid mindmap and flowchart can be imported into a draft process with explicit limitations recorded.
- Published processes can be exported as Mermaid and JSON packages.
- Starter templates can reference prompt-flow patterns without making Prompt Factory the canonical process store.
- Import warnings are explicit whenever semantics do not round-trip perfectly.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessImportExportService.cs (new)`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs (reference pattern)`
- `output/prompt-library/factory-prompt-flow-templates.seed.json`
- `tests/CanDoItAll.Tests.Integration/ProcessImportExportIntegrationTests.cs (new)`
