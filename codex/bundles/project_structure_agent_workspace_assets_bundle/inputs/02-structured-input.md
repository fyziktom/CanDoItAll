# Structured Input

## Profile

- Bundle profile: `initiative`
- Reason: The request crosses agent configuration, runtime tool composition, project-structure MCP guidance, storage-driver access, and validation.

## Objectives

- Allow a technical agent to be explicitly granted one or more external filesystem workspace roots for code analysis and edits.
- Make Mermaid and file output semantics unmistakable for both internal project-structure tools and the external ProjectStructure MCP.
- Expose storage-driver-backed read/write tools to agents through the same agent-settings pattern used by project-structure and process access.
- Preserve or improve native workspace file read tools so agents can browse, search, and read files without shell workarounds.

## Hard Constraints

- External folders must be per-agent, explicit, and normalizable to the existing `external-target/<drive>/...` alias model.
- Read/write controls must be visible in agent settings and enforced in runtime tool composition or tool guards.
- Mermaid diagrams added to project structure must be represented as `ProjectObjectType.File` with `objectSubtype = "mermaid"` and Mermaid source in `notes`; metadata should allow diagram-kind detection.
- Storage tools must honor catalog enablement, storage read-only flags, driver capability masks, and per-agent read/write policy.
- Existing project-structure and process default internal tools must continue to work.

## Assumptions

- The first implementation can support filesystem-style external roots via the existing `external-target` alias system.
- Storage-driver tools can provide catalog list, text read, text write, and delete first; deeper remote browsing can follow only if the driver contracts grow list/stat APIs.
- UI browser proof is helpful but not mandatory for the first closure if component/unit tests cover the settings model and tool composition. If UI layout is materially changed, browser proof must be recorded.

## Risks

- Broad external-drive access can be dangerous if guards are not enforced below tool descriptions.
- Storage driver contracts do not currently expose directory listing across all providers.
- Existing tests may assume file tools are capability-driven; default access must avoid unintentional broad tool attachment.
