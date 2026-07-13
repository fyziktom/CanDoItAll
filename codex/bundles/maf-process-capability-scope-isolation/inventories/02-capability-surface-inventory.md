# Capability Surface Inventory

## Current Capability Selectors

| Selector | Existing support | Notes |
| --- | --- | --- |
| Capability kind | Supported by `CapabilitySelector.ByKind` | Useful for broad skill/MCP/tool suppression. |
| Capability key | Supported by `CapabilitySelector.ByCapabilityKey` | Good for catalog skill/tool/MCP keys. |
| Capability tag | Supported by `CapabilitySelector.ByTag` | Existing catalog descriptors include catalog/kind/tags/classification tags. |
| Operation classification | Supported by `CapabilitySelector.ByOperationClassification` | Used by process allowed-operation policy. |
| Runtime tool name | Supported by `CapabilitySelector.ByRuntimeToolName` | Primary selector for workspace/runtime tools. |
| MCP server key | Supported by `CapabilitySelector.ByMcpServerKey` | Primary selector for MCP server suppression. |
| MCP tool name | Supported by `CapabilitySelector.ByMcpToolName` | Primary selector for single MCP tool suppression. |
| Implementation key | Supported by `CapabilitySelector.ByImplementationKey` | Useful if consistently populated. |
| Runtime tool provider key | Not first-class today | Add provider-key tag or implementation-key mapping before exposing provider-level policy. |

## Current Effects

| Effect | Current behavior | Implementation caution |
| --- | --- | --- |
| `Deny` | Suppresses matching candidates. | Use for suppression. |
| `Require` | Reports missing required capability when no allowed candidate matches. | Pass non-empty required capabilities from runtime context. |
| `Allow` | Not restrictive in the evaluator today. | Do not model process allowlists with plain `Allow` unless evaluator/compiler changes. |

## Existing Runtime Context Signals

| Signal | Current owner | Limitation |
| --- | --- | --- |
| `AllowedOperations` | Process assignment and execution metadata | Controls operation classes, not named skill/MCP suppression. |
| `BrowserToolsAllowed` | Process metadata and runtime context | Broad switch only. |
| `WorkspaceToolsEnabled` | Runtime metadata and context | Broad switch only. |
| `RuntimeToolProvidersEnabled` | Runtime metadata and context | Broad switch only. |
| `WorkspaceToolProfile` | Process cooperation metadata | Workspace-tool profile only. |

## Target Added Signals

- Effective process-step capability directives.
- Required capability identities or selectors.
- Scoped instruction fragments with ownership and capability prerequisites.
- Provider identity tags or implementation keys for runtime provider tools.
- Manifest diagnostics that include process run id, step id, rule id, selector, and reason.
