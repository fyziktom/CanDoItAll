# Assumptions and Risks

## Working Assumptions

- The current extracted repository is the intended post-refactor state to review.
- The repository bundles and ADRs are relevant architectural context, not fully trusted proof.
- The upcoming plugin wave is expected to be large enough that architecture debt should be treated now, not later.

## Critical Path Risks

- A plugin wave started on the current base will likely deepen split truth and subtype fragmentation.
- Future connectors may copy the current metadata and enum/switch patterns if they remain available.
- If persisted SyncGraph stays in place, plugin contributors will be tempted to project themselves into Workbench canonical tables.

## Validation Risks

- Runtime validation is blocked here because `dotnet` is unavailable in the container.
- Some issues might be even more visible at runtime (especially around orchestration and load performance).

## Reopen Triggers

Reopen this review immediately if any of the following happen before the planned refactor wave:

- a new integration is added through another enum value or switch branch
- a plugin proposal needs a new GUID/reference field inside Workbench metadata
- SyncGraph gains another contributor that writes system-managed nodes into Workbench tables
- another large responsibility is added to `ProjectWorkbenchModels.cs`
