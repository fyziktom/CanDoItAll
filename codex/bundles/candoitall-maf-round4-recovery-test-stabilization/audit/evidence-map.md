# Evidence Map

All line numbers are from the attached snapshot and may shift after implementation. Do not copy the secret value from `appsettings.json` into any output.

## Secret evidence

- `src/CanDoItAll.Web/appsettings.json:33` contains an `OPENAI_API_KEY` value matching a real provider-key pattern. The value is redacted here by design.
- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs` was not found.

## Missing claimed round 3 files/classes

Searches over `src/` and `tests/` did not find:

- `AgentRecoveryModels.cs`
- `AgentRecoveryModelsTests.cs`
- `SecretScanningTests.cs`
- `AgentReworkPacket`
- `ProofFingerprint`
- `RecoveryLedger`

Codex must verify these exist after implementation before claiming success.

## Tool policy classification

`src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs:227-237`

`IsMutationTool(...)` currently contains workspace mutation tools only:

- `workspace_dotnet_new`
- `workspace_pwsh_run_script`
- `workspace_python_run_file`
- `workspace_create_directory`
- `workspace_write_file`
- `workspace_append_file`
- `workspace_copy_path`
- `workspace_move_path`
- `workspace_delete_path`

No process mutation tools are listed.

## Process tools

`src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs:49-139` creates process tools via `AIFunctionFactory.Create(...)`.

Read-like tools include:

- `processes_definitions_list`
- `processes_definition_editor_get`
- `processes_definition_export`
- `processes_runs_list`
- `processes_run_detail_get`
- `processes_analytics_get`
- `processes_party_options_list`
- `processes_executor_options_list`
- `processes_templates_list`
- `processes_template_get`
- `processes_template_mermaid_get`
- `processes_template_baseline_scenarios_list`

Mutation tools include:

- `processes_definition_save`
- `processes_definition_publish`
- `processes_definition_delete`
- `processes_definition_import`
- `processes_run_start`
- `processes_step_transition`
- `processes_assignment_resolve`
- `processes_artifact_record`
- `processes_template_import`

`src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:184-205` attaches process tools directly with `composition.State.Tools.AddRange(tools)`.

## Recovery loop

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:25-346`

Observed behavior:

- retries the current step rather than the whole process;
- carries a set of successful tool names across attempts;
- often resets `automationChatSessionId` to force a fresh session;
- builds a text recovery directive for subsequent attempts.

## Recovery directive

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs:25+`

The directive is long and text-first. It contains missing tools, critical tool failures, domain/project hints, and detailed retry guidance. It is not a typed recovery decision or typed rework packet.

## Playwright Release/no-build issue

- `tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs:52` starts the app with `dotnet run --no-build` and no `--configuration Release`.
- `tests/CanDoItAll.Tests.Playwright/DatabaseSwitchWorkbenchPlaywrightTests.cs:130` has the same pattern.
- `tests/CanDoItAll.Tests.Playwright/WebGlSandboxPlaywrightFixture.cs` already uses a better Release-aware pattern and can be used as a reference.

## MCP hardcoded path issue

- `tests/CanDoItAll.Tests.Integration/ProcessesMcpStdioIntegrationTests.cs:13-14` hardcodes `C:epositories\CanDoItAll` and a Debug assembly path.
- `tests/CanDoItAll.Tests.Integration/ProjectStructureMcpStdioIntegrationTests.cs:11-12` has the same issue.

## Finalizer and exception-boundary improvements

- `MafAgentRuntime.AgentFactory.cs` now passes finalizer mode into finalizer capture creation.
- `AgentToolPolicyBlockedException` exists in `AgentToolInvocationPolicy.cs`.

These are positive changes and should be preserved.
