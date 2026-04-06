# Strengths and fixed areas

The review is not a blanket rejection of phase9. Several important changes appear materially real:

## 1. Legacy node carrier retirement is materially implemented
`src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:27-58`
shows that `ProjectObjectRecord` now persists:
- canonical node basics,
- `MarkersJson`,
- `MetadataJson`,
- `ParentNodeKey`,
- coordinates and timestamps,

and keeps binding/reference runtime state as `[NotMapped]` members:
- `Binding`,
- `NodeReferences`.

The legacy route/media/external-artifact carrier is no longer persisted on `ProjectObjectRecord`.

## 2. Binding data is composed through a dedicated binding record/state model
`src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs`
shows a dedicated persistence and runtime model:
- `ProjectNodeBindingRecord`,
- `ProjectNodeBindingState`,
- `ProjectNodeReferenceRecord`.

The active binding logic no longer writes back into old carrier fields on the node entity.

## 3. Shared manifest-driven connector editor exists
`src/CanDoItAll.Modules.Workspace/Pages/Components/ConnectorConfigFieldEditor.razor`
renders field editors by `ConnectorConfigFieldType`, and the page-level key switches in shared resource/provider pages are gone.

## 4. Custom plugin identity is materially plugin-key first
Save flows in:
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`

persist plugin identity from the resolved plugin and no longer synthesize a fake legacy enum from the editor model for custom plugins.

## 5. Node references are open-world in the persistence contract
`ProjectNodeReferenceRecord` now stores:
- `ReferenceKind` as string,
- `ReferenceId` as string,
- `OrderIndex`.

That is a real improvement over the earlier closed enum contract.

## 6. Generic connector command/outbox boundary exists
The repo contains a generic connector-command boundary and dedicated tests around idempotency/retry/replay behavior. That part of phase9 looks materially present.
