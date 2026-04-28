# Target Solution

## Architecture Shape

Add a reusable project transfer core in the workbench module because the portable project aggregate spans `CanDoItAll.Modules.Projects` and `CanDoItAll.Modules.Workbench`.

Recommended implementation objects:

- `ProjectsDatabaseTransferHandler` implements `IDatabaseTransferHandler`.
- `ProjectPackageService` exports/imports `.zip` packages and delegates table inventory/copy ordering to the same lower-level project transfer core as the database handler.
- A small model file describes package manifests, table payloads, and results.

## Layering

- `CanDoItAll.Modules.Projects` owns CRUD for project cards and hierarchy basics.
- `CanDoItAll.Modules.Workbench` already references `Projects` and owns the project structure/workbench graph, so it is the correct module for a transfer handler that needs both sets of entity types.
- `CanDoItAll.Infrastructure.ControlPlane` remains the generic transfer contract owner; avoid hard-coding project-specific transfer into infrastructure.
- UI should consume services from existing module pages rather than introducing HTTP endpoints unless browser download/upload requirements force one.

## Data Rules

- Copy project core records first: projects, phases, options, hierarchy links.
- Copy project object records before links, bindings, references, lifecycle events, view states, projection layouts, and cross-module mutation records.
- Clear target records in reverse dependency order.
- Preserve IDs so project hierarchy, node links, node references, and media binding metadata remain coherent.
- Ensure project/workbench schema initializers have run for both source and target contexts before counting or copying.

## Zip Package Shape

Expected package entries:

- `manifest.json`
- `tables/<sort>-<table>.json` for project-scoped table payloads
- `storage/managed-files/...` for project media files when referenced and available

The manifest should include package format, created time, source profile id, table names, row counts, and warnings.

## UI Shape

- Database-to-database: registering `ProjectsDatabaseTransferHandler` should add a `Projects` row to the existing transfer item grid in the data-sources page and the new managed SQLite startup prompt.
- Zip: add compact controls to the Projects board/page header for exporting all projects and importing a package path. Use existing BaseLib controls and the app's restrained operational style.
