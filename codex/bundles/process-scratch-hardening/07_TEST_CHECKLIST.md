# 07 — Test Checklist (automated, regression lanes, UI/API procedures)

Tick each line when it passes and record the evidence (command, count, output excerpt, screenshot)
in `proof/VALIDATION-LOG.md`. Before touching a failing existing test, read the ledger
([06](06_BEHAVIOR_CHANGE_LEDGER.md), INV-3).

**Common setup:**
- Integration and Components need the isolated PG18 connection:
  ```powershell
  $envs = docker inspect candoitall-maf122-tests-3d729670 --format '{{range .Config.Env}}{{println .}}{{end}}'
  # set CANDOITALL_TESTS_POSTGRES_CONNECTION to Host=127.0.0.1;Port=55004;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>;Database=postgres (never print the password)
  ```
- Build: `dotnet build <changed src csproj> -c Release /m:1`, then
  `dotnet build <test csproj> -c Release /m:1 --no-dependencies` while 5032 runs.
- Run: `dotnet test <test csproj> -c Release --no-build --filter "<filter>"`. Output is in Czech:
  `Úspěšné` = passed, `Neúspěšné` = failed.

---

## Stage A — new tests

| ID | Layer | Test (class.method or description) | Proves |
| --- | --- | --- | --- |
| T-A1 | Unit | `WorkspaceProcessStartFailureClassifierTests` (new): Win32 2 → ExecutableNotFound; 267 with a missing folder → WorkingDirectoryMissing; 267/206 with an existing folder > 258 (Windows) → WorkingDirectoryTooLong; 5 → AccessDenied; Unix errno ENOENT (2) with a missing folder → WorkingDirectoryMissing; EACCES (13) → ExecutableNotRunnable; ENAMETOOLONG (36/63) → WorkingDirectoryTooLong; resolution `Missing`/`NotExecutable`/`ForeignPathSyntax` → kinds; a non-start exception → Unknown | classification per OS |
| T-A2 | Unit | `WorkspaceLaunchExplanationsTests` (new): every kind except `None` has code, agent text and operator remediation; codes are unique with the prefix `ProcessStartFailed.`; agent text has no drive root, UNC or `/home`/`/Users`-style path; `CanRetryWithCorrectedInput` is true only for WorkingDirectoryMissing | table completeness, INV-2 |
| T-A3 | Unit | `LocalWorkspaceProcessHostTests` (extend): a real start of a non-existent executable path → `Started=false`, kind ExecutableNotFound, `FailureMessage` without a path; a missing working folder → WorkingDirectoryMissing; Windows-only: a working folder longer than 258 → WorkingDirectoryTooLong (created with a `\\?\` path, cleaned up after) | real host behaviour |
| T-A4 | Unit | `WorkspaceCommandExecutionServiceTests` (extend): a fake host returning StartFailed(kind) → message `could not start … (ProcessStartFailed.<Kind>)`, `FailureCode` set, **no** "Inspect captured diagnostics" hint; a pwsh script start failure records rejected-before-effect; a resolution failure returns a result (no throw) | BC-A1/2/4/5 |
| T-A5 | Unit | `MafRuntimeToolInvocationResultClassifier` test (new or extend the nearest classifier test): JSON with `failureCode` → assessment FailureCode and canRetry per the explanation; a Mutation pre-start kind → NotCommitted | trace/journal evidence |
| T-A6 | Unit | Dotnet lifecycle: a fake host throwing `WorkspaceProcessStartException(kind)` → result code; a resolution failure → ExecutableNotFound | M3 |
| T-A7 | Unit | `ProjectStructureRuntimeAdapterTests` (extend): the host throws a start exception with WorkingDirectoryTooLong → the launch result message has the remediation and the folder path (operator surface) | runtime nodes |
| T-A8 | Unit | `ProcessRuntimeFailureClassifier` test: text containing `ProcessStartFailed.` → HostLaunch; `ProcessExecutionResultFactoryTests`: HostLaunch → `process.adapter.agent_tool_launch_failed`; `ProcessRuntimeOperatorDiagnosticDetailsBuilderTests`: gate id and NextAction | process runs |
| T-A9 | Unit / Components | Chat stream: an unsuccessful tool result → an execution step with the code (`MafStreamingTurnExecutor…` unit, or `ChatWorkspacePanelTests` rendering) | chat |
| T-A10 | Integration | `MafGenericToolAdmission…` style: an agent calls `workspace_pwsh_run_script` and the fake host start fails → the trace has the code, effect NotCommitted, and the run is not blocked by an Unknown effect | end to end with MAF |

## Stage A — regression lane R-A

| Lane | Filter | Expected |
| --- | --- | --- |
| R-A.U | Unit: `LocalWorkspaceProcessHostTests\|WorkspaceCommandExecutionServiceTests\|WorkspaceExternalProcessRunnerTests\|ProjectStructureRuntime\|ProcessExecutionResultFactoryTests\|ProcessRuntimeOperatorDiagnosticDetailsBuilderTests\|ProcessRuntimeFailureClassifier\|MafStreamingTurnExecutor\|ManagerProcessOwnershipTests\|McpRuntimeContractsTests\|DockerHostToolServicePortabilityTests\|WorkspaceProcessStartFailure\|WorkspaceLaunchExplanations\|WorkspaceManagedScriptPlanExecutorTests\|DotNetSolutionSetupRuntimeExecutorTests` | all pass |
| R-A.UF | **Whole Unit assembly** | all pass (catalog parity checks live here) |
| R-A.I | Integration: `MafGenericToolAdmission\|MafWorkspaceToolResultDisclosure\|ProjectStructureAgentIntegrationTests\|AgentFrameworkExecutionRunTracking\|ProcessCapabilityPortability\|ExecutionFoundationPortability` | all pass |
| R-A.C | Components: `ChatWorkspacePanelTests\|ProjectStructurePage\|EmbeddedBrowserTests` | all pass |

## Stage B — new tests

| ID | Layer | Test | Proves |
| --- | --- | --- | --- |
| T-B1 | Unit | `WorkspaceCommandEnvironmentPolicyTests` (extend): toolchain from the executable (`dotnet.exe`, `/usr/bin/dotnet`, `pwsh`, `powershell.exe`, `python3`, `py.exe`, `node`, `npx.cmd`, `git`, `docker`, `other.exe`) × Windows/Linux gives the right tool list; the tool name is the fallback when the executable is empty | BC-B1 |
| T-B2 | Unit | Never-empty: an explicit null or `""` for TEMP/TMP/TMPDIR/HOME is dropped and diagnosed; other explicit nulls are dropped | BC-B2 |
| T-B3 | Unit | Unix home guarantee: `HOME` missing → the toolchain home under the injected state root; `HOME` present → unchanged; Windows → never applied | BC-B3 |
| T-B4 | Unit | Options: NetworkTrust off → proxy/CA names absent; on → present; `AdditionalInheritedNames` containing `OPENAI_API_KEY`, `DOTNET_STARTUP_HOOKS`, `MY_TOKEN` or `LD_PRELOAD` → dropped and logged; a valid name → passed; extended names reach script tools only with `extendedEnvironment: true` | D4, BC-B7 |
| T-B5 | Unit | `ProjectStructureRuntimeAdapterTests` terminal: Windows arguments contain the prelude that removes non-composed variables and the explanation; Unix arguments start with `env -i` and the composed pairs; no `OPENAI_API_KEY` in either | BC-B4, INV-5 |
| T-B6 | Unit | `WorkspaceScriptEnvironmentAccessAnalyzerTests` (new). PowerShell: `$env:TEMP`/`$env:PATH` → no finding; `$env:OPENAI_API_KEY`, `${env:MY_SECRET}`, `[Environment]::GetEnvironmentVariable('X')` → named finding; `Get-ChildItem env:`, `gci Env:`, `dir env:`, `[Environment]::GetEnvironmentVariables()` → enumeration; `$env:$name`, `Get-Item "env:$n"` → dynamic. Python: `os.environ['HOME']`, `os.getenv('TEMP')` → none; `os.environ.get('OPENAI_API_KEY')` → named; `dict(os.environ)`, `os.environ.items()`, `for k in os.environ`, `print(os.environ)` → enumeration; `os.environ[name]`, `os.getenv(var)` → dynamic. Writes (`$env:X = 1`) → none | B9 |
| T-B7 | Unit | `AgentToolInvocationPolicyTests` (extend): gate inactive (default) → the existing decision; gate active with a finding → Deny, and the reason names the finding and the setting "Scripts may read environment variables"; gate active with an inspection failure → Deny; a non-script tool → unaffected; the governed-script tests stay green | B10, BC-B6 |
| T-B8 | Unit | `AgentWorkspaceToolAccessMetadataTests` (extend): `canScriptsReadEnvironment` round trip; **only this flag set → the section is written (`IsDefault`)**; Normalize keeps it false when scripts are off | B8 trap |
| T-B9 | Unit | `AgentEditorDraftPolicyTests`, seed normalizer equality (difference detected), `RuntimeCapabilityComposer` override carries the flag, `MafAgentRuntimeToolProviderCompositionTests`: the policy context flag equals the setting | B8 wiring |
| T-B10 | Components | `AgentDetailsDialogSettingsTests` (extend): toggling scripts on opens the confirmation; cancel → stays off; acknowledge and confirm → on, and the warning alert is visible; the env checkbox is disabled while scripts are off; enabling env shows its warning | B7, B8 UI |
| T-B11 | Components | `HrAgentWorkspaceToolAccessAdministrationTests` (extend): HR input/patch/read-back carries the new flag | B8 |
| T-B12 | Integration | A real agent turn (MAF fake provider, like `MafGenericToolAdmission`): scripts on, env off, the script reads `$env:OPENAI_API_KEY` → a PolicyDenied result with remediation **before any process starts** (no command reaches the process host); env on → the command executes | end to end |
| T-B13 | Unit | Added after UI-B2 (BC-B9): `RunScopedRecoveryAndScriptInspectionBaselineTests.Script_policy_inspection_reads_a_script_in_a_root_the_effective_access_allows`. A chat run without run aliases plus effective access with the agent's configured root → the script is inspected; a root outside the effective access → refused with a clear reason | BC-B9 |
| T-B14 | Integration `[HostPlatform]` | Added for UI-B3 (BC-B11): `ProjectStructureTerminalEnvironmentParityTests`. The presenter's generated command runs in a real shell (pwsh on Windows, `env -i` on Linux/macOS) with a dummy secret in the parent; the runtime sees only composed names, no secret name or value | BC-B11, INV-5 |

## Stage B — regression lane R-B

| Lane | Filter | Expected |
| --- | --- | --- |
| R-B.U | Unit: `WorkspaceCommandEnvironmentPolicyTests\|ProjectStructureRuntimeAdapterTests\|AgentToolInvocationPolicyTests\|AgentWorkspaceToolAccessMetadataTests\|AgentEditorDraftPolicyTests\|MafAgentRuntimeToolProviderCompositionTests\|McpPortabilityPolicyTests\|McpRuntimeContractsTests\|DockerHostToolServicePortabilityTests\|WorkspaceGitCommandExecutorTests\|SandboxWorkspaceSeed\|RunScopedRecoveryAndScriptInspectionBaselineTests\|MafSkillResultDisclosureTests\|WorkspaceScriptEnvironmentAccessAnalyzer\|LocalWorkspaceProcessHostTests\|WorkspaceExternalProcessRunnerTests` | all pass |
| R-B.UF | Whole Unit assembly | all pass |
| R-B.C | Components: `AgentDetailsDialog\|HrAgentWorkspaceToolAccessAdministrationTests\|AgentSeamFinalizationTests\|ProjectStructurePage` | all pass |
| R-B.I | Integration: `McpExternalToolPortability\|ExecutionFoundationPortability\|MafWorkspaceToolResultDisclosure\|MafSdkSkillAdmission\|AgentEditorAdapterIntegrationTests\|AgentFrameworkWorkspaceSeedIntegrationTests\|MafGenericToolAdmission\|AgentTeamCatalogIntegrationTests\|ProjectStructureAgentIntegrationTests` | all pass |

## Stage C — new tests

| ID | Layer | Test | Proves |
| --- | --- | --- | --- |
| T-C1 | Unit | Redirect decision (plugin level): a read-only external alias → `.build/<key>`; writable alias → none; workspace target → none; switch off → none; the key is deterministic and **identical for a `.sln` and a `.csproj` under the same read-only root** | C1 |
| T-C2 | Unit | `WorkspaceCommandExecutionServiceTests` (extend): restore/build/test/run/run-http/publish with `artifactsPath` → `--artifacts-path <full>` is present exactly once; without it → **absent** (explicit assertion) | C2, BC-C1 |
| T-C3 | Unit | Message appendix present only when redirected; the receipt target references include `.build/<key>` | C3 |
| T-C4 | Unit | `MafAgentRuntimeToolProviderCompositionTests` decorator compiles and forwards `artifactsPath` | BC-C2 |
| T-C5 | Integration `[HostPlatform]` | `WorkspaceDotnetReadOnlyTargetTests` (new): a real tiny console and test project in an external folder bound **read-only** → build, then `test --no-build`, then run (no HTTP): all succeed; the SHA-256 of the external tree (all files) is unchanged; `.build/<key>/bin` exists. The same project writable → `bin`/`obj` in the project and no `.build` | the core guarantee on real dotnet |
| T-C6 | Integration `[HostPlatform]` | A second run (new execution) with `test --no-build` finds the output from the first run (persistence, INV-1) | "product not deleted" |

Implemented as `WorkspaceReadOnlyBuildOutputTests` (Unit: T-C1…T-C3 plus the plugin wiring) and `WorkspaceDotnetReadOnlyTargetTests` (Integration: T-C5, T-C6 in the same class; the test project pins the package versions of the integration project so restore is served from the local cache). T-C4 is covered by the compiling decorator.

## Stage C — regression lane R-C

| Lane | Filter | Expected |
| --- | --- | --- |
| R-C.U | Unit: `WorkspaceCommandExecutionServiceTests\|WorkspaceExternalTargetAliasTests\|WorkspacePublishedOutputTests\|LocalWorkspaceProcessHostTests\|MafAgentRuntimeToolProviderCompositionTests\|ScenarioHarnessAgentRuntimeTests\|WorkspaceRuntimePlugin` | all pass |
| R-C.UF | Whole Unit assembly | all pass |
| R-C.I | Integration: `WorkspaceDotnetReadOnlyTargetTests\|ProjectStructureAgentIntegrationTests\|MafWorkspaceToolResultDisclosure\|ExternalTargetAlias` | all pass |

---

## CI-parity gate (added after CI run 36270125343)

The impact search for the behaviour ledger covered `tests/Unit`, `tests/Components` and
`tests/Integration` but **not `tests/Playwright`**. The local lanes also never ran the stable
job's runtime portability gate, which includes a Playwright smoke test. BC-A12 therefore
broke `ProjectStructureRuntimeNodePlaywrightTests` on all three OSes. Before closing any
change to user-visible text or runtime behaviour:

- search `tests/Playwright` (and `tests/Playwright/GovernanceBrowserFixture`) for every changed
  string and test id;
- run the stable job locally in CI order: build (both slnx plus `Test-RuntimePortability.ps1
  -BuildOnly`), steps 12–16, Chromium install, `Test-RuntimePortability.ps1 -SkipBuild`, then
  `Test-CorePortabilityHeadless.ps1`, the documentation check and the install-script tests (steps
  CI skips when an earlier stable step fails);
- run it on Windows and in the Linux container (siblings at the CI commits; the runtime gate
  needs Git checkouts).

## UI and API procedures (Windows, 5032)

**Preparation (once per stage):**
- Stop 5032, build the web host, and restart 5032 with stdout captured (the memory recipe).
- Create a dedicated **"Hardening Lab"** project through the Project Structure API. Do not use
  Tetris3 for destructive or odd cases.
- Record every created node or project in the validation log.
- Large screen (~1680×950).

| ID | Procedure | Expected |
| --- | --- | --- |
| UI-A1 | Lab project: create a folder longer than 258 characters (with a `\\?\` path) containing a console `.csproj`; add a .NET runtime node pointing at it; Run | Quick-action or feedback text says the working folder path is N characters, names the limit, and gives the remediation. The 5032 log contains the operator detail |
| UI-A2 | Lab project: a Direct command node whose executable does not exist; Run | "… was not found on PATH …" plus the remediation |
| UI-A3 | Chat with a scripts-enabled agent: ask it to build a non-existent project and then run a script with a missing interpreter (if feasible) | The chat execution stream shows "Tool '…' failed: <code> — …"; the agent explains the reason from the typed code |
| UI-B1 | Agents → an agent → Workspace tools: toggle "Can run local scripts" on | The confirmation dialog appears; Cancel keeps it off; acknowledge and Confirm turns it on and shows the warning alert. "Scripts may read environment variables" is disabled while scripts are off; enabling it shows its warning. Save, reload, and the settings persist |
| UI-B2 | Chat with that agent (env **off**): "Create and run a PowerShell script that prints `$env:OPENAI_API_KEY` and lists `Get-ChildItem env:`" | Blocked before the script runs (with approvals on, the refusal follows the approval click; with auto-approval, it is immediate); the chat shows the reason naming the setting. Turn env **on**, retry: it runs; the output lists only allowlisted names; `OPENAI_API_KEY` is empty |
| UI-B3 | Lab runtime node → Open terminal; run `echo $env:OPENAI_API_KEY; gci env: \| measure` | Empty key; the explanation line is shown; the count is small (the composed set) |
| UI-B4 | (Optional) Restart 5032 with `…__NetworkTrust=true` and a dummy `HTTPS_PROXY`; repeat UI-B2 with env on | `HTTPS_PROXY` is visible to the script; with env off it is absent. With NetworkTrust off, a failed restore shows the NetworkTrust hint |
| UI-C1 | Chat: select the Tetris3 node that has an external output folder (**read-only** through the project block); ask the agent to build and test the Tetris3 project | Succeeds; the message names `.build/<key>`; the product tree hash before and after is identical (PowerShell `Get-FileHash` over all files, recorded) |
| UI-C2 | **Full delivery process**: launch `blazor-app-delivery` via `POST /api/processes/launch/check`, then `/launch`, on the Lab project with a fresh output folder; follow it in Live processes; read history and receipts via the API | The run completes; review steps on read-only targets leave the product unchanged; implementation steps build normally; no unexplained failures; the app starts from its runtime node afterwards (**its output still exists**, INV-1) |
| API-1 | `GET /api/processes/runs/{id}/history` for UI-C2, and one receipt per stage from the artifacts | Codes, messages and redirects are visible; no physical paths in agent-facing texts |

## Linux (Docker, local)

- **L-1:** in `candoitall-maf122-validation:local`, from a `git archive` of the working tree
  (including untracked new files: copy them in explicitly), build and run the **whole Unit
  assembly**. Expect all pass. This covers the Unix errno mapping, env/HOME composition and
  terminal `env -i`.
- **L-2:** with a `postgres:18.6-alpine` container on an explicit-subnet network (the memory
  recipe), run the Integration filter
  `WorkspaceDotnetReadOnlyTargetTests|ProcessCapabilityPortability|ExecutionFoundationPortability|McpExternalToolPortability`.
- **L-3:** run a missing working folder and an unset `HOME` through a small console harness,
  or through T-A3/T-B3 running on Linux, and record the messages.

## macOS (CI, the operator runs it)

- **M-1:** trigger CI with `platform-scope: full` (or rely on the HostPlatform split). In the log
  zip, check that these pass on macOS:
  - the Unit assembly (T-A1/T-A3 errno mapping including ENAMETOOLONG=63, T-B3, T-B5);
  - `WorkspaceDotnetReadOnlyTargetTests`;
  - `LocalWorkspaceProcessHostTests`.
  Share the log zip for review.
