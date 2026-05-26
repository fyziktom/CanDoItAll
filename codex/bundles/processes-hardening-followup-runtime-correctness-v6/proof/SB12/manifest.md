# SB12 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` defines `ProcessDefinitionContractMode`.
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs` persists contract mode on `ProcessDefinitionVersion`.
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260526023209_ProcessDefinitionContractMode.cs` adds the PostgreSQL `ContractMode` column with existing rows defaulting to `Compatibility`.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs` resolves effective strict lint from request mode, version contract mode, criticality, and autonomy.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs` enforces strict lint on publish.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` enforces strict lint on run start.
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs` escalates text-inferred risky operation contracts to errors in strict mode.
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs` supports string enum values in strict template operation contracts.
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json` and sibling Blazor templates declare typed operation contracts and artifact recovery policy text.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs` and `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` cover strict, compatibility, publish, run-start, and template migration behavior.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB12 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs | bundle://proof/SB12/manifest.md | bundle://proof/SB12/transcripts/passing.txt | bundle://proof/SB12/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB12/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB12/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB12/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB12/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB12/transcripts/changed-file-hashes.txt`

- `B98A85832DE2179B0EAEC6F6C6EB760A8F0610CE729DFB814DCAB7C04635948D` `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~SB12_INV|FullyQualifiedName~ProcessDefinitionLinterTests|FullyQualifiedName~PublishAsync_SB10_INV_001_applies_strict_lint|FullyQualifiedName~StartRunAsync_SB10_INV_001_applies_strict_lint|FullyQualifiedName~Blazor_process_templates_project_with_required_runtime_browser_and_writeback_contracts"` passed: 22 tests.
- `dotnet build src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --no-restore` passed with existing EF Core relational version MSB3277 warnings.
- `dotnet ef migrations add ProcessDefinitionContractMode --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext --output-dir Migrations` succeeded; the EF tools/runtime version warning is unrelated.
- SQLite audit found no SB12 SQLite runtime or migration dependency.
- Migration XML-doc audit found no generated XML documentation comments in the SB12 migration files.

## Blockers

None.




