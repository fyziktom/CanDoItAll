# Structured Input

## Goal

Prepare an initiative bundle to migrate skills, tools, and MCP servers from hardcoded MAF/persistence code into isolated projects and file-driven templates.

## Must Preserve

- Existing default agent/team capability assignments.
- Existing runtime tool names, especially `workspace_*`, `browser_*`, provider-native, finalizer, process, project-structure, and image-generation names.
- Existing skill activation behavior for file, inline, and registered skills.
- Existing local/hosted/http MCP behavior, secret binding restrictions, allowed tool filtering, and approval semantics.
- Existing process/workflow behavior that depends on tool policy and capability filtering.

## Must Change In Implementation

- Create dedicated abstractions and implementation projects before reconnecting MAF.
- Move default skills/tools/MCP capability definitions into `Templates/Capabilities` or an equivalent sibling under `Templates/`.
- Support internal and external tools with a generic call contract.
- Support internal and external MCP servers with setup validation and list-tools testing.
- Add UI and API setup flows for tools, and strengthen MCP test-start/list-tools setup flow.
- Split tests into unit, integration, and e2e layers.

## Quality Constraints

- Keep strongly typed contracts in C#; template JSON/YAML must deserialize into validated typed models.
- No silent fallback behavior: invalid templates, unavailable tool implementations, missing MCP allowed tools, and failed setup tests must return explicit validation results.
- New projects should group related implementation folders by capability domain, for example `FileSystem`, `DotNet`, `Documents`, `Images`, `Processes`, `ProviderNative`, and `External`.
- Preserve compatibility first; cleanup and deletion of old hardcoded branches happens only after regression proof.
