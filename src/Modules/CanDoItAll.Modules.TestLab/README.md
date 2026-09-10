# CanDoItAll.Modules.TestLab

## Purpose

Product module for test plans, cases, evidence metadata, and recorded execution results. The `/test-lab` page edits these records, links plans to projects and responsible parties, and exposes their latest recorded result. Evidence stores an artifact path; this service does not upload evidence bytes or execute a universal test runner.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj --configuration Release /m:1
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.TestLab.csproj](CanDoItAll.Modules.TestLab.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

`TestLabService` uses a short-lived `TestLabDbContext` containing only `TestPlan`, `TestCaseRecord`, `TestEvidenceRecord`, and `TestRunRecord`. Its pooled factory is configured from the immutable `ICanonicalRuntimeDatabase.Profile`. The complete application schema reuses the same four mapping configurations and remains the sole migration authority; this context introduces no separate schema, migrations, or data copy. These records have no application-managed concurrency tokens.

Saving a plan commits its cases, evidence metadata, and runs together, then updates search and records activity. These post-commit calls retain their existing behavior: a search or activity failure can surface after the plan is already saved. Project and responsible-party IDs remain references; TestLab does not own those records, and a recorded test result does not automatically accept a task.

The project-transfer target-state participant temporarily retains its existing complete-schema maintenance read under the transfer coordinator. Workbench's test-plan projection and node-scope bridge also retain their current read integration. These remaining reads do not change TestLab's runtime writer and must be replaced by owner queries in their respective boundary slices.

## Focused Validation

`TestLabOwnerPersistenceTests` covers the exact owner model, complete-schema mapping parity, foreign-query rejection, historical aggregate readback after restart, owner edits preserving IDs, and profile isolation. Existing callers remain covered by `CrmHrCrossModuleIntegrationTests`, `ProjectStructureAgentIntegrationTests`, and `ProjectStructureAutomaticPlacementIntegrationTests`.

Follow [the repository testing procedure](../../../docs/testing.md) to build, confirm discovery counts, and execute the focused filters. Source changes also require portability-static enforcement; this README does not assert that any test has passed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
