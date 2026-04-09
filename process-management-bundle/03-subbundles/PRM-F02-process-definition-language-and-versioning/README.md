# PRM-F02 — Process definition language and versioning

## Objective

Create the canonical process DSL with definitions, versions, nodes, transitions, lifecycle status, publication rules, and template cloning semantics.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 1**
- Depends on: **PRM-F01**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Process definitions support draft, published, and archived versions.
- Published versions are immutable and draft edits produce a new working version.
- Definitions can be scoped as workspace templates or project-owned processes.
- The canonical graph is stored outside Workbench metadata.

## Non-goals

- Do not hide process graph semantics in JSON blobs if they need cross-entity querying.
- Do not allow published versions to be edited in place.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessDomain.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessVersioningServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessesSchemaInitializer.cs (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs (new)`
- `tests/CanDoItAll.Tests.Unit/ProcessDefinitionLanguageTests.cs (new)`
