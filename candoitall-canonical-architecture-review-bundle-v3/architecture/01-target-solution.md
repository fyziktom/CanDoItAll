# Target Solution

## Core Repair

- Extend `IProjectPartyIntegrationBridge` with canonical node-assignment lifecycle operations that Workbench can call safely:
  - replace node assignments for a node/role set
  - delete assignments for removed node keys
  - move assignments to a target project for transferred node keys

## Read/Write Behavior

- `ProjectStructurePage` reads editor state from assignment rows.
- `ProjectStructurePage` writes canonical assignments first through the bridge.
- Workbench metadata remains a derived view and is updated after canonical assignment changes to keep previews readable.

## Lifecycle Behavior

- `ProjectWorkbenchService.DeleteObjectAsync` removes assignment rows for the deleted node keys through the bridge.
- `ProjectWorkbenchService.MoveDescendantsToProjectAsync` moves assignment rows for the transferred node keys through the bridge.

## Validation Shape

- Targeted integration tests prove delete and transfer reconciliation.
- Existing structure-page component/browser flows prove the editor still works with assignment-first truth.
- The integrated architecture-review skillset is rerun after implementation to confirm the critical split-source finding is actually resolved.
