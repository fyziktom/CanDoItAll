# SB02 Semantic Invariants

## SB02-INV-001

- Invariant ID: `SB02-INV-001`
- Source raw note: `N002`
- Expected behavior: Development database reset targets only process-owned tables and process activity history.
- Disallowed shallow implementation: Dropping the whole database, truncating broad public tables, or deleting project/agent/plugin/memory/workspace rows.
- Failing-first test: `bundle://proof/SB02/transcripts/db-before-counts.txt`
- Passing test: `bundle://proof/SB02/transcripts/db-after-counts.txt`
- Changed source files: `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`; `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- Production assertions: Target list comes from information_schema and contains only `Processes_` table names; process category activity cleanup is explicit.
- Red-team negative case: A cross-domain foreign key or non-process table name would block the reset plan.
- Downstream dependency check: Representative non-process counts were captured before and after cleanup.

## SB02-INV-002

- Invariant ID: `SB02-INV-002`
- Source raw note: `N002`
- Expected behavior: Current default process templates reload through application warmup and all eight are published.
- Disallowed shallow implementation: Inserting rows manually, accepting draft-only templates, or reloading stale generated artifacts.
- Failing-first test: `bundle://proof/SB02/transcripts/template-reload.txt`
- Passing test: `bundle://proof/SB02/transcripts/template-reload.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs`; `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateEditorModelFactory.cs`; `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackModels.cs`
- Production assertions: Warmup gate reported `definitions|versions|steps|published=8|16|134|8`.
- Red-team negative case: A half-published catalog failed the readiness gate and forced template fixes before final reload.
- Downstream dependency check: Subprocess child artifact references resolve from child step/title metadata during import.

## SB02-INV-003

- Invariant ID: `SB02-INV-003`
- Source raw note: `N003`
- Expected behavior: Agents, plugins, memory, projects, project structure, and workspace settings remain intact.
- Disallowed shallow implementation: Assuming preservation without before/after database counts.
- Failing-first test: `bundle://proof/SB02/transcripts/db-before-counts.txt`
- Passing test: `bundle://proof/SB02/transcripts/non-process-preservation.txt`
- Changed source files: `repo://codex/bundles/processes-ui-options-dev-db-reset-v10/proof/SB02/transcripts/non-process-preservation.txt`
- Production assertions: Representative preserved table counts are equal before and after the reset/reload.
- Red-team negative case: Any changed preserved count would fail closure and require restore/investigation.
- Downstream dependency check: Project hierarchy, workbench project objects, plugin connections, workflow definitions, and workspace settings counts were compared.
