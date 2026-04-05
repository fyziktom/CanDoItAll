# Current State

## Live Repo Observations

- No CRM/HR module project exists yet at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr`.
- Module composition currently flows through `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ModuleAssemblies.cs`, and per-module registration.
- Workbench already exposes participant, meeting, work-item, dependency, and storage-aware node behavior through `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureDependencyAnalysis.cs`.
- Storage integration is driver-backed through `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Placement\StoragePlacementService.cs` and `StorageObjectReference` helpers.
- Test projects already exist for unit, component, integration, and Playwright validation.

## Bundle Drift Repairs Applied

- Added a workflow-aligned lowercase bundle structure without deleting the architect's original uppercase package.
- Added `scripts/validate_bundle.py` compatible with `--stage prepared` and `--stage completed`.
- Added execution-ready subbundle contracts under `subbundles/` that point at the live repo files and the preserved architect item docs.
- Elevated the latest repo facts called out by the user: storage-driver usage, node dependency APIs, participant linkage, and AI profile separation.

## Immediate Execution Implications

- B01 and B10 are higher risk than the original bundle assumed because they must preserve current Workbench node metadata semantics while introducing central-party linkage.
- Any file or media fields added by CRM/HR need to route through the storage-placement pipeline rather than bypassing current storage providers.
- UI subbundles must validate against the current shell and BaseLib surfaces, not the older page assumptions frozen in the architect bundle.
