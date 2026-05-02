# Assumptions And Risks

## Assumptions

- Technical agent settings are the right user-facing control point for external workspace roots and storage-driver access.
- `external-target/<drive>/...` remains the canonical runtime-facing representation for external folders, even when a user enters an absolute Windows path in settings.
- Storage access should be layered on existing `IStorageCatalogService` and `IStorageDriverRegistry` rather than inventing a parallel storage client.
- File browse/search/read tools may remain capability-driven for existing agents, but new access settings must let an operator grant external workspace roots and storage access explicitly.

## Critical Path Risks

- External alias controls must not become description-only; runtime guards must deny external paths outside configured aliases.
- A write-enabled external alias must not silently permit sibling repositories or parent directories outside the selected root.
- Storage catalog read/write settings must account for disabled, read-only, and capability-limited storage records.
- Project-structure Mermaid guidance must reach both internal tools and the MCP surface, or external agents will continue creating generic architecture/work-item nodes.

## Validation Risks

- A full Blazor UI browser pass may be heavy; if component tests can prove settings save/load, UI proof can be recorded as not applicable unless layout is changed substantially.
- Storage-driver tool tests need a filesystem storage fixture; IPFS and FTP can be contract-level only because they depend on external services.
- Existing runtime tests may need updates if new default tool attachment changes visible tool sets.

## Reopen Triggers

- Reopen subbundle 01 if any file command or workspace-file tool can touch `external-target` paths outside the configured per-agent aliases.
- Reopen subbundle 02 if Mermaid source can still be written through project-structure tools without landing as `ProjectObjectType.File` + `objectSubtype = "mermaid"` or without explicit guidance.
- Reopen subbundle 03 if storage tools ignore per-agent settings, storage catalog read-only flags, or driver capability masks.
- Reopen subbundle 04 if tests only validate descriptions and do not exercise runtime guards or settings round-trip behavior.
