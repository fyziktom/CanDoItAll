# SB02 Proof Manifest

## Status

- Result: `Passed`
- Scope: Processes workspace hidden-section lazy loading.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs` defines deferred-load flags for executor options, workflow options, party options, analytics, and improvements.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs` removes the old hidden-section eager sequence from initial load and introduces explicit ensure methods.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.DefinitionCrud.cs` and `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.TemplateLibrary.cs` refresh dependent analytics only after mutations that need current data.
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs` asserts hidden runtime and analytics data remain unloaded until the relevant tab requires them.

## Semantic Contract

- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB02/transcripts/tests-passing.md`.
- Failing-first: N/A process because the user supplied runtime-latency symptoms rather than a standalone failing executable case; the negative probe verifies the removed eager-call sequence stays absent.
- Negative probe transcript: `bundle://proof/SB02/transcripts/negative-probe.md`.
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.md`.
- Test name: `Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it`

## Changed-File Hashes

- `C01A6BFAF879BB3A974EF9BAD7F36961E718F7B776990564917C5E11218EBCDE` `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs`
- `E1627AED5BC951DEE354A7F787DB9A247358CC4C247219311EAE8FB42FEB39E8` `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `4566FC4EC3FEC2D988908F536A0687C70C33039CA46F7CA726D9B993B4D822D1` `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

## Validation

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it|FullyQualifiedName~Workflows_page_defers_component_library_until_component_sections_need_it|FullyQualifiedName~Quick_sibling_note_insertion_persists_downward_stack_shift|FullyQualifiedName~Workflows_page_creates_starter_workflow_and_runs_preview|FullyQualifiedName~Workflow_canvas_places_llm_component_validates_runs_and_saves_definition" --no-build --no-restore -v:minimal` passed.

## Changed Files

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.DefinitionCrud.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.TemplateLibrary.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
