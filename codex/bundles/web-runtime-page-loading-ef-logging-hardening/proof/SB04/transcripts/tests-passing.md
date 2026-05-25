# SB04 Tests Passing Transcript

- Invariant ID: `WEB-SB04-001`
- Test name: `Workflows_page_defers_component_library_until_component_sections_need_it`
- Test name: `Workflows_page_creates_starter_workflow_and_runs_preview`
- Test name: `Workflow_canvas_places_llm_component_validates_runs_and_saves_definition`

Command:

```powershell
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it|FullyQualifiedName~Workflows_page_defers_component_library_until_component_sections_need_it|FullyQualifiedName~Quick_sibling_note_insertion_persists_downward_stack_shift|FullyQualifiedName~Workflows_page_creates_starter_workflow_and_runs_preview|FullyQualifiedName~Workflow_canvas_places_llm_component_validates_runs_and_saves_definition" --no-build --no-restore -v:minimal
```

ExitCode: 0

Output:

```text
Passed: 5
Failed: 0
```
