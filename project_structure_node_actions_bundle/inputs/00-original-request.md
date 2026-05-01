# Original Request

Source: user request in Codex desktop thread on 2026-04-30.

```text
Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this:
1) We have runtime nodes in project structure, but when I double click them it must offer to run that command as second option in that modal that opens after doubleclick in project structure canvas. And there must be also option to run command in the right click menu of that node. it must offer always two options: run normally and run as administrator. 
2) for file related nodes it must offer open in system file explorer if file is on drive. If it is on ipfs it must offer to open in new tab.
3) assure that all of those nodes and information about how they work are in the candoitall project structure mcp and also in the project structure tools for internal ai agents.
```

## Raw Notes

| Raw note | Exact wording | Normalized requirement ids | Owning subbundle |
| --- | --- | --- | --- |
| `N001` | "We have runtime nodes in project structure, but when I double click them it must offer to run that command as second option in that modal that opens after doubleclick in project structure canvas." | `REQ-RUN-001`, `REQ-RUN-002` | `01-runtime-node-run-actions` |
| `N002` | "And there must be also option to run command in the right click menu of that node. it must offer always two options: run normally and run as administrator." | `REQ-RUN-003`, `REQ-RUN-004` | `01-runtime-node-run-actions` |
| `N003` | "for file related nodes it must offer open in system file explorer if file is on drive." | `REQ-FILE-001`, `REQ-FILE-002` | `02-file-and-ipfs-open-actions` |
| `N004` | "If it is on ipfs it must offer to open in new tab." | `REQ-FILE-003`, `REQ-FILE-004` | `02-file-and-ipfs-open-actions` |
| `N005` | "assure that all of those nodes and information about how they work are in the candoitall project structure mcp and also in the project structure tools for internal ai agents." | `REQ-TOOLS-001`, `REQ-TOOLS-002`, `REQ-TOOLS-003` | `03-mcp-and-internal-agent-action-contracts` |

## Literal Scope

- `must offer always two options` is preserved for runtime-capable nodes: run normally and run as administrator must appear together in the double-click modal and right-click menu when a runtime launch plan resolves.
- `file related nodes` is preserved as file or media artifact nodes that expose managed file metadata, storage references, media paths, or artifact routes.
- `on drive` is interpreted as a trusted local or managed file-system path that the existing path guard allows.
- `on ipfs` is interpreted as `StorageProviderKind.Ipfs` storage references or absolute IPFS-backed routes.
