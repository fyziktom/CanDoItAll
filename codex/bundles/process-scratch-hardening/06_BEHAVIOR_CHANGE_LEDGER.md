# 06 — Behaviour Change Ledger

This file lists **every** intended behaviour change in stages A–C. When an existing test fails,
look here first (INV-3):

- **Listed** → update the test exactly as the "Test action" column says.
- **Not listed** → the implementation drifted. Fix the code and keep the test.

Also listed are the tests that must **not** change, because they guard behaviour that looks
similar but is kept.

## Stage A

| ID | Old behaviour | New behaviour | Tests asserting the old behaviour → action |
| --- | --- | --- | --- |
| BC-A1 | Start failure message `Recipe '<id>' failed to start: The configured workspace process could not be started.` | `Recipe '<id>' could not start: <agent text, program names for exe kinds> [ProcessStartFailed.<Kind>]` (the code comes last so the chat preview's two-sentence cut keeps the reason) | None found (the map checked `could not be started`, `failed to start`, `Started: false`). New T-A4 pins the new text. |
| BC-A2 | A start failure always appends "Inspect captured diagnostics … stdout: …; stderr: …" | Omitted when not started **and** stdout/stderr are empty; kept otherwise | **Keep unchanged:** `WorkspaceCommandExecutionServiceTests.DotnetTest_failure_result_points_agent_to_captured_diagnostics` (:2060; a started failure with exit code 1). |
| BC-A3 | `WorkspaceCommandExecutionResult` has no failure code | New positional-safe member `FailureCode` (init property, default `""`); JSON gains `failureCode` | Tests that construct or deconstruct the record positionally must still compile. It is an init property, not positional, so no change is expected. |
| BC-A4 | A Mutation script tool that failed to start → effect `Unknown` | Pre-start kinds (not `ProcessOwnershipFailed`) → rejected before effect (`NotCommitted`); `FailureCode` = the code | Tests asserting `Unknown` for failed script **starts**: none found. **Keep unchanged:** tests asserting `Unknown` for scripts that started and failed. |
| BC-A5 | Executable resolution failure inside the runner or dotnet lifecycle throws (opaque `ToolInvocationFailed`) | Returns a not-started result with `ExecutableNotFound` / `ExecutableNotRunnable` | If a test expects a throw from `ExecuteAsync` for a missing executable, update it to expect `Succeeded=false` plus the code (search `WorkspaceExecutableResolutionException` in tests). |
| BC-A6 | Runtime node start failure: "The runtime executable could not be started on this host." | Kind-specific operator text with the path and remediation | None found; T-A7 pins it. |
| BC-A7 | A process step whose tool failed to start → `process.adapter.agent_execution_failed` | → `process.adapter.agent_tool_launch_failed`, with its own gate id and `NextAction` | **Keep unchanged:** existing `ProcessExecutionResultFactoryTests` and `ProcessRuntimeOperatorDiagnosticDetailsBuilderTests` cases for other kinds. |
| BC-A8 | The chat stream shows only "Invoking tool …" | Also "Tool '<name>' failed: <code> — <text>" for unsuccessful results with a code | Components tests that count execution steps exactly (`ChatWorkspacePanelTests`) → only if a scenario includes a failed tool; update the count with a comment referencing BC-A8. |
| BC-A9 | `LocalWorkspaceProcessHost` has no logger | Optional logger (constructor default) | Existing `new LocalWorkspaceProcessHost()` call sites stay valid. |
| BC-A10 | The local MCP start failure says "…could not start its approved executable." with a fixed hint | The message adds `(ProcessStartFailed.<Kind>)`; the hint is the kind's remediation (the fixed hint stays for Unknown) | None found (searched the message and hint text in tests). |
| BC-A11 | The host resolves the executable before its `try`, so a resolution failure throws `WorkspaceExecutableResolutionException` from `StartSessionAsync` | It throws `WorkspaceProcessStartException` (ExecutableNotFound/NotRunnable). Callers already resolve first, so this is rare | `McpPortabilityPolicyTests.cs:213` tests the Playwright resolver's own locator (not the host) → **keep unchanged**. |
| BC-A12 | The runtime-node resolver says "The required executable dependency is missing, inaccessible, or invalid for this host." for every resolution failure | A missing or unrunnable program says so, with the program name and remediation; other failures keep the old text | None found. |
| BC-A13 | When the Windows `subst` alias for a long workspace path cannot be created, `WorkspaceCommandProcessRunner.ExecuteAsync` throws `InvalidOperationException`. The tool invocation then leaves the admission journal "uncertain" (found by T-A10) | A typed no-effect start failure `ProcessStartFailed.WorkingDirectoryTooLong` with rejected-before-effect; the operator detail names the alias failure | None found; new unit test `A_long_workspace_path_whose_drive_alias_cannot_be_created_is_a_typed_no_effect_failure`. |
| — | Attach or fingerprint failure after `Process.Start` throws `WorkspaceProcessStartException` | Unchanged; the kind is `ProcessOwnershipFailed` | **Keep unchanged:** `LocalWorkspaceProcessHostTests.cs:25` (`ThrowsAsync<WorkspaceProcessStartException>`). |

### Repairs to earlier uncommitted work (found by `HostPlatformTestClassificationTests` in stage A)

| Item | Action |
| --- | --- |
| `ProjectStructurePageWebPreviewTests` writes real files (the runtime-node csproj test from the earlier repair) | Class-level `[Trait("Category", "HostPlatform")]` added (correct classification, not a weakening) |
| `MafGenericToolAdmissionIntegrationTests.PolicyReason.cs` contained a Windows drive path in its reason text | The reason text now says "the raw folder path"; the test still proves the reason reaches the model |
| The earlier agent-path guidance ("A project-structure chat can read only … ask the user to select this node") is also returned to HTTP API node creation, where it is misleading (found in UI-A1 preparation) | The message now names both valid sources (workspace folder, or the project block's declared folder) and keeps the chat hint as a conditional sentence. `ProjectStructureRuntimeLauncherPathResolverTests` asserts "ask the user to select" → still contained, so it stays unchanged |

## Stage B

| ID | Old behaviour | New behaviour | Tests → action |
| --- | --- | --- | --- |
| BC-B1 | Environment profile by tool name only (static serve and skill scripts get the common list only) | Toolchain from the resolved executable; the tool name is the fallback | **Keep unchanged:** all 7 `WorkspaceCommandEnvironmentPolicyTests` (INV-4). `ProjectStructureRuntimeAdapterTests.Direct_adapter_inherits_the_toolchain_environment…` (:44) keeps its assertions; if it references `ResolveEnvironmentProfile`, retarget it to the executable-based path with the same expectations. |
| BC-B2 | A null explicit value → empty variable in the child | Dropped (not set) | Tests expecting `""` for a null value: search `EnvironmentVariables` with null values in tests; update per the ledger if found. |
| BC-B3 | Unix child without `HOME` when the host lacks it | `HOME`/`DOTNET_CLI_HOME` = `<StateRoot>/toolchain-home` | None (new). |
| BC-B4 | Terminal presentation inherits the whole host environment | Only the composed set, plus the explanation line | `ProjectStructureRuntimeAdapterTests` terminal argument tests: update the expected argument prefix (Windows prelude / Unix `env -i`) and keep all other expectations. |
| BC-B5 | Enabling "Can run local scripts" happens silently | A confirmation dialog when enabled manually; a warning alert while enabled | Components tests that toggle `agents-catalog-workspace-scripts`: none today. Profile-change tests keep working (no dialog on a profile change; the alert only). |
| BC-B6 | Script tools run whatever the content does with the environment | Gate: env reads outside the allowlist, enumeration, or dynamic names → Deny unless `CanScriptsReadEnvironment`; inspection failure → Deny while the gate is active. Enforced when the call executes, before the process starts (after an approval click when approvals are on) | Policy tests build `ToolInvocationPolicyContext` directly. The default `ScriptEnvironmentAccessAllowed = true` keeps the existing ones unchanged. **Keep unchanged:** the 16 governed-script policy tests in `AgentToolInvocationPolicyTests` (:1568–2017). Integration tests that run scripts through a real agent with default settings: if a fixture script reads `$env:`/`os.environ` outside the allowlist, **grant the setting in that fixture's agent**, not by weakening the analyzer. |
| BC-B7 | Extended names (none existed) | NetworkTrust and additional names are passed only when configured; to scripts only with the agent permission | None. |
| BC-B8 | Settings JSON has no `canScriptsReadEnvironment` | New key, default false; omitted when default | `AgentWorkspaceToolAccessMetadataTests` round trips keep passing; the Maf120 durable journal fixtures are frozen and must still load (**do not edit them**). |

### Repair found by the Linux lane L-1

| ID | Found in | Defect | Repair | Tests → action |
| --- | --- | --- | --- | --- |
| BC-A14 | L-1 (whole Unit on Linux): `WorkspaceLaunchExplanationsTests.Program_names_are_added_without_their_folders`, `WorkspaceProcessEnvironmentCompositionTests.The_toolchain_follows_the_program…(C:\Program Files\dotnet\dotnet.exe …)` | `Path.GetFileName` does not split on `\` on Linux/macOS. A persisted Windows executable path evaluated there came back whole: the launch message would put a physical folder into agent text (INV-2), and the toolchain fell back to the tool name | `WorkspaceLaunchExplanations.ProgramFileName` takes the last segment after `/` or `\`, used by `WithProgramNames` and `ResolveToolchain` | None: both tests already expressed the intended cross-host behaviour and now pass unchanged |

### Stage B repairs found by the UI procedures

| ID | Found in | Defect | Repair | Tests → action |
| --- | --- | --- | --- | --- |
| BC-B9 | UI-B2 (chat, env off) | The script inspection read only the **run's** external-target aliases. In a chat, a script in the agent's **configured** external root (usable by the workspace tools) could not be read, so every such script was refused as "could not be inspected … outside the current run external-target boundary", including scripts without environment access | `MafScriptPolicyInspectionService.ResolveScriptContentInspectionForPolicy` takes the invocation's effective access (last optional parameter), which the factory passes. Governed runs are unchanged, because their effective access excludes configured roots | New `RunScopedRecoveryAndScriptInspectionBaselineTests.Script_policy_inspection_reads_a_script_in_a_root_the_effective_access_allows`; existing inspection tests unchanged |
| BC-B11 | UI-B3 real-shell test (`ProjectStructureTerminalEnvironmentParityTests`) | The Windows terminal prelude removed variables with `[System.Environment]::SetEnvironmentVariable($name, $null)`. PowerShell passes `$null` to a string parameter as `""`, so every host variable **name** stayed defined with an empty value (137 names, among them `OPENAI_API_KEY` with no value). No values leaked, but the terminal did not get the composed set | `Remove-Item -LiteralPath ('Env:' + $name)`, verified in pwsh 7 and Windows PowerShell 5.1 (11 kept names, nothing else) | `ProjectStructureRuntimeAdapterTests.Windows_terminal_prelude_removes_every_variable_outside_the_composed_environment`: expected prefix extended with the removal command (this bundle's own test); new real-shell integration test |
| BC-B10 | UI-B2 | Refusal text "reads X; lists all environment variables" read awkwardly | `Describe` joins as a sentence: "reads A and B, lists all environment variables and …" | `WorkspaceScriptEnvironmentAccessAnalyzerTests.The_description_names_variables_and_kinds_without_values`: expected text updated (this bundle's own test) |

## Stage C

| ID | Old behaviour | New behaviour | Tests → action |
| --- | --- | --- | --- |
| BC-C1 | dotnet restore/build/test/run/publish write `bin`/`obj` into a read-only external target | `--artifacts-path <workspace>/.build/<key>` is added only for read-only external targets | **Keep unchanged:** every existing argument assertion (`WorkspaceCommandExecutionServiceTests` :265-299, :943-1029, :1978-2253; `WorkspaceExternalTargetAliasTests.cs:351-356`; `WorkspacePublishedOutputTests`). They use workspace or writable targets, so no `--artifacts-path` appears (T-C2 asserts its absence explicitly). |
| BC-C2 | Service method signatures | A new **last** optional parameter `artifactsPath` | `MafAgentRuntimeToolProviderCompositionTests.cs:3300-3306` (the decorator forwarding signatures) → add the parameter to the decorator. `ScenarioHarnessAgentRuntimeTests.cs:112-115` reads positional `args[3]`, which is unaffected because the new parameter comes last. |
| BC-C3 | Result message | Appendix naming `.build/<key>` for redirected commands only, including failed ones, so the agent knows where the output of a later `--no-build` lives | None. |
| BC-C4 | `IWorkspacePublishedOutputCommands.DotnetPublish` | A new **last** optional parameter `artifactsPath`. `WorkspacePublishedOutputRuntime` computes it itself and ignores a caller's value | No test implements the interface (checked). |
| BC-C5 | Redirect key | The key comes from the SHA-256 of the normalized read-only **root alias** instead of the physical path, a documented deviation from C1. A versioned alias carries the registry's root identity | None. |
| — | `LocalWorkspaceProcessHostTests.cs:50-77` source-text test on `BuildDotnetRun` | Unchanged; it must still find `executableCandidates: ["dotnet"]` and no pwsh | **Keep unchanged.** |
