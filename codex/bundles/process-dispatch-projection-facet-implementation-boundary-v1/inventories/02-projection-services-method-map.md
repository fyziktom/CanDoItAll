# Projection Services Method Map

Current source:
`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionServices.cs`

Known issue:
- One nested class implements all projection facets.
- Most methods forward to `ProcessRunAutomationDispatchService`.
- File IO methods are mixed with pure classification/matching/lineage methods.

Codex must create a final method map with:
- old method,
- target facet implementation,
- pure vs side-effect classification,
- tests covering behavior,
- source scan proof.
