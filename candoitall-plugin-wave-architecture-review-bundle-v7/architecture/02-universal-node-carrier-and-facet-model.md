# Universal node carrier and facet model

## Intent

Keep the node as the user's stable mindmap-native anchor.

## Proposed split

### Carrier

A lean `ProjectNode` carrier owns:
- `NodeId` / `NodeKey`
- `ProjectId`
- `ParentNodeId` or `ParentNodeKey`
- `ActiveKindKey`
- `Title`
- `Subtitle`
- `Notes`
- `Status`
- `Priority`
- `PositionX`
- `PositionY`
- canonical marker representation
- optional schedule anchors
- timestamps

### Facets

Examples:
- `ProjectNodeWorkItemFacet`
- `ProjectNodeMeetingFacet`
- `ProjectNodeParticipantFacet`
- `ProjectNodeRepositoryFacet`
- `ProjectNodeEnvironmentFacet`
- `ProjectNodeInfrastructureFacet`

### Bindings

Examples:
- `ProjectNodeArtifactBinding`
- `ProjectNodeMediaBinding`
- `ProjectNodeStorageBinding`
- `ProjectNodeResourceBinding`
- `ProjectNodeProviderBinding`
- `ProjectNodeSecretBinding`
- `ProjectNodePartyBinding` (if needed beyond CRM/HR assignment ownership)

## Important constraints

- Stable node identity must survive reclassification.
- X/Y and markers stay canonical.
- Metadata cannot become a hidden foreign-id store again.
