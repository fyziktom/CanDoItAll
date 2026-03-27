# Selection Panel Node Audit

## Confirmed from Baseline

- File nodes can show both subtype text and subtype badge, which is unnecessary duplication.
- Upload-related status is also repeated between badges and nearby panel content.

## Audit Focus for Execution

- Review `SelectedNodeLeadText` in `ProjectStructurePage.CreateCatalog.cs`.
- Review `BuildFacts` in `ProjectStructureNodeDescriptor.cs`.
- Review badge generation in `ProjectWorkbenchModels.cs`.
- Verify whether non-file node types also repeat guidance or metadata that should move behind a help affordance.

## Success Condition

- Each node type should present one clear primary label, one concise supporting detail set, and only the hints that materially help the user act.
