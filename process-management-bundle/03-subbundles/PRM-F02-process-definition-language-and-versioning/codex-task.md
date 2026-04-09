# Codex task — PRM-F02

Implement **Process definition language and versioning** inside the uploaded CanDoItAll solution.

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

- Process definitions support draft, published, and archived versions.
- Published versions are immutable and draft edits produce a new working version.
- Definitions can be scoped as workspace templates or project-owned processes.
- The canonical graph is stored outside Workbench metadata.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessDomain.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessVersioningServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessesSchemaInitializer.cs (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs (new)`
- `tests/CanDoItAll.Tests.Unit/ProcessDefinitionLanguageTests.cs (new)`
