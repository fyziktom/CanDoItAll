# CanDoItAll Process Template Pack

This directory is the repository-owned process template pack consumed by the Processes runtime. It is application input, not generated documentation and not a Codex bundle.

## Source Of Truth

- `manifest.json` identifies the pack, process definitions, shared resources, toolbox catalogs, baseline scenarios, and live-run profiles.
- `processes/<key>/definition.json` is the canonical definition for one template.
- `shared/` contains reusable roles, artifacts, checklists, validations, and prompts.
- `toolbox/` contains reusable role and step authoring inputs.
- `seed-catalog/` contains regression scenarios and internal live-run launch profiles.

`ProcessTemplatePackLoader` validates the manifest, definitions, referenced guidance, role usage, dependency graphs, artifacts, and launch metadata when it loads the pack. Do not maintain generated Markdown or Mermaid sidecars as a second definition.

## Authoring Rules

- Keep process and step keys stable, unique, and file-driven.
- Model role assignments, step dependencies, required artifacts, branch outcomes, workflow outputs, subprocess outputs, approvals, and recovery guidance in the definition instead of hard-coded runtime branches.
- Treat baseline scenarios as regression fixtures. A real operator run must use current-run transitions and artifacts.
- Keep templates product-neutral where possible. Put concrete target details in launch variables, assignments, or managed artifacts.
- Do not grant mutation authority through prose. `AllowedOperations` and `OperationTargetScope` values must come from `ProcessOperationContractNames.AllOperations` and `ProcessOperationContractNames.AllTargetScopes`.
- Use the narrowest operation and target-scope contract. Planning and review normally remain read-only; implementation or repair owns product mutation; project-structure writeback is an explicit external action.
- Keep runtime dispatch generic. Add reusable typed contracts or launch contributors when a new template requires behavior that does not fit the current model.

The owning contract catalog is:

```text
src/MAF/Common/CanDoItAll.AgentFramework.Models/Contracts/ProcessOperationContractNames.cs
```

## Runtime And Authoring Surfaces

The `/processes` and `/projects/{projectId}/processes` Blazor workspaces expose the definition catalog and typed editors. The `/api/processes` HTTP family launches, controls, and reads runs; it does not expose process-template authoring endpoints.

Live-run profiles are loaded internally by `ProcessTemplatePackLoader` and used by launch preparation. There is no public `/api/processes/templates/live-run-profiles` route and no first-party `processes_template_live_run_profiles_list` runtime tool provider.

When public Processes behavior changes, update the maintained API documentation here and the canonical Processes API skill in `CanDoItAll.SharedInfo`. Do not add a product-repository copy of the Codex development skill.

## Validation

Run the template projection and loader regression suite from the repository root:

```powershell
dotnet test .\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProcessDefinitionCatalogProjectionTests" /m:1
```

Then run the stable repository gate from [Testing](../../docs/testing.md).
