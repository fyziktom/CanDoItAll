# SB05 Proof Manifest

## Status

- Result: `Passed`
- Scope: EF console logging option and final web runtime validation.

## Source Assertions

- `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs` adds the strongly typed `DatabaseOptions.EnableEntityFrameworkConsoleLogging` option, which defaults to `false`.
- `repo://src/CanDoItAll.Web/Program.cs` applies EF command and infrastructure logging filters when the option is false.
- `repo://src/CanDoItAll.Web/appsettings.json` and `repo://src/CanDoItAll.Web/appsettings.Development.json` explicitly keep EF console logging disabled by default.
- `repo://tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs` covers default and configuration binding behavior.
- `repo://artifacts/web-runtime-hardening-startup.out.log` and `repo://artifacts/web-runtime-hardening-startup.err.log` are the host startup logs captured during final validation.

## Semantic Contract

- Semantic invariants: `bundle://proof/SB05/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB05/transcripts/tests-build-startup-passing.md`.
- Failing-first: N/A process because this subbundle adds a configuration hardening guard and validates it through tests plus startup log inspection.
- Negative probe transcript: `bundle://proof/SB05/transcripts/negative-probe.md`.
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.md`.
- Test name: `DatabaseOptions_DisablesEntityFrameworkConsoleLogging_ByDefault`
- Test name: `DatabaseOptions_BindsEntityFrameworkConsoleLoggingSwitch`

## Changed-File Hashes

- `2EF679DE9236CAE3B8B306C5C66E238690CFF56C9055223221E65E370093D4BC` `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `712823D8ABF04C4F8208C01DF19200FBC85692AFB967B0E4B118674537091305` `repo://src/CanDoItAll.Web/Program.cs`
- `E666FADA9E790539F5974DEF3B0B49EA8AEFA78EEAC29742A091D7E4723DE03B` `repo://src/CanDoItAll.Web/appsettings.json`
- `2D0A117FADEBB645277714FAD0F7C111BD0125E5BB7C543201A1E90A1B0C9325` `repo://src/CanDoItAll.Web/appsettings.Development.json`
- `ABC80DE579E1D16259038CB542E9287B53D74E6E46023DB75B6745EB307B0370` `repo://tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs`

## Validation

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~DatabaseConfigurationTests" --no-restore -v:minimal` passed.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it|FullyQualifiedName~Workflows_page_defers_component_library_until_component_sections_need_it|FullyQualifiedName~Quick_sibling_note_insertion_persists_downward_stack_shift|FullyQualifiedName~Workflows_page_creates_starter_workflow_and_runs_preview|FullyQualifiedName~Workflow_canvas_places_llm_component_validates_runs_and_saves_definition" --no-build --no-restore -v:minimal` passed.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal` passed with existing MSB3277 EF Core relational version warnings.
- Web host startup reached the dev readiness endpoint and EF command-log match count was `0`.

## Changed Files

- `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `repo://src/CanDoItAll.Web/Program.cs`
- `repo://src/CanDoItAll.Web/appsettings.json`
- `repo://src/CanDoItAll.Web/appsettings.Development.json`
- `repo://tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs`
