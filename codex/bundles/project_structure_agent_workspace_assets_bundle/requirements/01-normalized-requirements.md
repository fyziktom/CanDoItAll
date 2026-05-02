# Normalized Requirements

## Requirements

- `REQ-01`: Agent settings must allow selecting external filesystem workspace roots for a technical agent.
- `REQ-02`: Selected external workspace roots must normalize to `external-target/<drive>/...` aliases so runtime tools can read, search, browse, build, test, and optionally edit cloned repositories outside the managed workspace.
- `REQ-03`: Runtime file and command tools must enforce configured external roots and must deny unconfigured external aliases.
- `REQ-04`: ProjectStructure MCP and internal project-structure tools must clearly instruct agents to create Mermaid diagrams as `ProjectObjectType.File` nodes with `objectSubtype = "mermaid"` and Mermaid source in `notes`.
- `REQ-05`: ProjectStructure tool guidance must similarly instruct file outputs to use typed file nodes, not generic work items or architecture blocks.
- `REQ-06`: Agents must have storage-driver-backed tools available through a default internal tool family, controlled by agent settings.
- `REQ-07`: Agent settings must expose storage read/write controls and allowed storage catalogs.
- `REQ-08`: Storage tools must honor per-agent read/write settings, disabled storage records, storage read-only flags, and driver capability masks.
- `REQ-09`: Agents must retain standard workspace file tools for searching, browsing/listing, and reading files.
- `REQ-10`: The implementation must include tests for settings serialization, runtime tool attachment/guards, Mermaid guidance, and storage read/write policy.

## Out Of Scope

- Cross-platform external alias UX beyond the existing Windows-drive alias mapping.
- Provider-independent storage directory listing because the current `IStorageDriver` contract does not expose list/stat.
- Full remote FTP/IPFS live integration tests.
