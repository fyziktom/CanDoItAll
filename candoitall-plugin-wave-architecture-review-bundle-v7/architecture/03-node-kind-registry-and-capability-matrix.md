# Node-kind registry and capability matrix

## Why

Node semantics are currently scattered. They need one authoritative registry.

## Proposed descriptor

Each kind descriptor should define:

- `KindKey`
- `Family`
- `DisplayName`
- `LegacyObjectType` mapping during transition
- `AllowedChildKinds`
- `AllowedRelationKinds`
- `AllowedPartyRoles`
- `AllowedCommands`
- `EditorSchemaKey`
- `FacetOwner`
- `TransitionTargets`
- `IsAssignable`
- `IsReclassifiable`
- `IsReadOnlyProjection`

## Required consumers

The following layers must consume the same registry:

- create palette
- create request composer
- node editor
- reclassification service
- CRM/HR node-scoped assignment validation
- MCP/tool exposure
- future plugin hooks

## Closure target

After the refactor, page code and CRM/HR should no longer decide node-role capability rules with private switch statements.
