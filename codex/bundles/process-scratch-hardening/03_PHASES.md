# 03 — Stages A–C: Work Items and Exit Criteria

The operator approved stages A–C on 2026-09-26, with extra requirements (see the handoff). Stage D
(managed temp folders) is **deferred**; its design stays in 02/05 for later. Each stage ships on
its own.

## Invariants (check before closing every stage)

| ID | Invariant |
| --- | --- |
| INV-1 | **No automatic deletion.** A–C add no code path that deletes files or folders. Redirected build output lives in a persistent workspace folder that no job deletes (`.build/`). |
| INV-2 | Agent-visible text never contains physical paths or raw exception text (`agent-tool-failure-recovery-boundary.md`). Operator surfaces (receipt files, runtime-node UI, logs) may. |
| INV-3 | **Only the behaviour changes listed in [06_BEHAVIOR_CHANGE_LEDGER.md](06_BEHAVIOR_CHANGE_LEDGER.md) are allowed.** When an existing test fails: if its old expectation is in the ledger, update the test as the ledger says; otherwise the implementation is wrong. Fix the code, never the test. |
| INV-4 | Every existing environment-exclusion test (`WorkspaceCommandEnvironmentPolicyTests`) stays unchanged and green. |
| INV-5 | After stage B, no launch mode passes host secrets to a child (managed, terminal, script). |
| INV-6 | Nothing in A–C blocks host startup. Invalid options are logged and ignored, never thrown. |
| INV-7 | Receipts keep saying `PolicyOnlyLocal (host-enforced: false)`. Script inspection is described as a guard rail, not a sandbox. |

**Rules:**
- One stage at a time. Update `proof/SESSION-HANDOFF.md` after each work item group and before
  long commands.
- Tick items here as they finish.
- No commits.
- Release builds with `/m:1` from PowerShell. While 5032 runs, build test projects with
  `--no-dependencies` after building the changed libraries.
- Integration uses the isolated PG18 container (`candoitall-maf122-tests-3d729670`, port 55004).

The test lists live in [07_TEST_CHECKLIST.md](07_TEST_CHECKLIST.md) (IDs T-A*, T-B*, T-C*, UI-*).

---

## Stage A — Explainable launches

**Goal:** a failed process start carries a reason kind. The operator sees the reason plus a
remediation; the agent sees a typed code plus sanitized text. No behaviour change beyond messages,
codes, logging and effect evidence (ledger BC-A*).

| # | Work item | Main files |
| --- | --- | --- |
| A1 | `WorkspaceProcessStartFailureKind` (`None, ExecutableNotFound, ExecutableNotRunnable, WorkingDirectoryMissing, WorkingDirectoryTooLong, AccessDenied, ProcessOwnershipFailed, Unknown`) and `WorkspaceProcessStartFailureClassifier` (Win32/errno codes, `WorkspaceExecutableResolutionException.Failure`, working-folder existence and length) | new `…/Workspace/Process/WorkspaceProcessStartFailure.cs` |
| A2 | `WorkspaceProcessStartException` gets `FailureKind` and `OperatorDetail`. `LocalWorkspaceProcessHost` classifies (resolution failures included) and returns `StartFailureKind` and `StartFailureDetail` on `WorkspaceProcessExecutionResult`. `FailureMessage` = agent-safe text. Optional `ILogger` logs the operator detail | `LocalWorkspaceProcessHost.cs`, `WorkspaceProcessContracts.cs`, DI sites |
| A3 | `WorkspaceLaunchExplanations`: kind → code `ProcessStartFailed.<Kind>`, agent text, operator remediation, `CanRetryWithCorrectedInput` | new `…/Workspace/Process/WorkspaceLaunchExplanations.cs` |
| A4 | Runner: message `Recipe '<id>' could not start: <agent text> (<code>)`; no "Inspect captured diagnostics" hint when not started and both streams are empty; resolution failures return a result instead of throwing; `WorkspaceCommandExecutionResult.FailureCode`; receipt `exitSummary` has the code and operator detail; not-started with a pre-start kind records rejected-before-effect (NotCommitted) | `WorkspaceCommandProcessRunner.cs`, `WorkspaceCommandToolModels.cs`, `WorkspaceCommandReceiptWriter.cs` |
| A5 | Classifier reads `failureCode`, and `canRetry` comes from the explanation | `MafRuntimeToolInvocationResultClassifier.cs` |
| A6 | Dotnet lifecycle uses the same classification (including resolution failures) | `WorkspaceDotnetProcessLifecycle.cs` |
| A7 | Runtime nodes: the adapter maps kind → operator text with the path and remediation. A missing-dependency message gets the remediation | `ProjectStructureRuntimeAdapters.cs` |
| A8 | Process runs: text containing `ProcessStartFailed.` → new failure kind `HostLaunch` → code `process.adapter.agent_tool_launch_failed` + summary; gate id and `NextAction` | `ProcessRuntimeFailureClassifier.cs`, `ProcessExecutionResultFactory.cs`, `ProcessRuntimeOperatorDiagnosticDetailsBuilder.cs` |
| A9 | Chat stream: an unsuccessful tool result with a failure code adds an execution step `Tool '<name>' failed: <code> — <agent text>`. If this proves invasive, record it as deferred with the reason (do not silently drop it) | `MafStreamingTurnExecutor.cs` / the tool result path |

**Exit:**
- T-A1…T-A9 pass.
- Regression lanes R-A pass.
- UI-A1…UI-A3 are done.
- Portability gate passes.
- INV-1…INV-7 checked.

---

## Stage B — Safe environment and script safety

### B-env

| # | Work item | Main files |
| --- | --- | --- |
| B1 | Toolchain by executable (`dotnet`, `python`/`py`/`python3`/`conda`, `pwsh`/`powershell`, `node`/`npm`/`npx`, `git`, `docker`, other). `MergeEnvironmentVariables(env, toolName, executablePath)` overload; the tool name stays as a fallback | `WorkspaceCommandEnvironmentPolicy.cs` |
| B2 | Never-empty rule (null or empty explicit values are dropped). Unix `HOME` guarantee (`<StateRoot>/toolchain-home`, mode 0700, only when `HOME` is missing). Diagnostics are names only | same, plus the state-root provider |
| B3 | `ProcessEnvironmentOptions` (`NetworkTrust`, `AdditionalInheritedNames`). Denylisted names are dropped and logged; startup is never blocked | new options, registration |
| B4 | Launchers pass the resolved executable: M1 runner, M3 dotnet lifecycle (fixes static serve), M8 runtime adapter (replaces `ResolveEnvironmentProfile`), M5 external, M6/M7 MCP, M9 docker, M4 git | the launcher files from 01 §2 |
| B5 | Terminal parity: Windows prelude removes variables that are not in the composed set; Unix `env -i`; one-line explanation | `ProjectStructureRuntimeAdapters.cs` (presenter) |
| B6 | `NetworkTrustNotPassed` hint: the host has proxy/CA names but NetworkTrust is off; shown in the failed-command message appendix and in the receipt | runner, policy diagnostics |

### B-script (operator requirement: setting + warning, proper tool, protection pipeline)

| # | Work item | Main files |
| --- | --- | --- |
| B7 | "Can run local scripts": a warning `Alert` while enabled, and a confirmation dialog with a risk acknowledgement when it is switched on manually (mirrors auto-approval). Profiles that enable scripts show the alert | `AgentDetailsDialog.razor(.cs)`, new `AgentLocalScriptsConfirmationDialog.razor` |
| B8 | New per-agent setting `CanScriptsReadEnvironment` ("Scripts may read environment variables"), default off. Enabled only when scripts are on, with warning text. Follows `CanReadStorage`: model and key, `Read`/`Write`/`Normalize`/**`IsDefault`**, editor draft, dialog, HR tools (input, patch, read-back), seed builder, loader, **normalizer equality**, carry-over in the `RuntimeCapabilityComposer` override | the files from the settings map (05 §2) |
| B9 | `WorkspaceScriptEnvironmentAccessAnalyzer` (PowerShell, Python): enumeration, dynamic names, and named reads outside the inherited allowlist for that tool | new, in Core/ToolPolicy |
| B10 | Policy step: `ToolInvocationPolicyContext.ScriptEnvironmentAccessAllowed` (default `true`; the only production construction site, the MAF factory, sets it from agent settings). For script tools with the gate active: a finding → **Deny** with reason and remediation naming the setting; inspection failure → Deny with reason. Runs **before the script process starts**: immediately when auto-approval is on, otherwise when the approved call executes (a pre-approval rejection would need changes to the approval/admission pipeline; recorded as a follow-up) | `AgentToolInvocationPolicy.cs`, `MafRuntimeAgentFactory.cs` |
| B11 | Hard shaping: extended names (NetworkTrust + additional) reach script-tool processes only when the agent has `CanScriptsReadEnvironment` (plugin → service optional parameter → plan) | `WorkspaceRuntimePlugin.cs`, `WorkspaceCommandExecutionService.cs`, plan builder |
| B12 | Docs: the `runtime-execution-portability.md` environment section; `secure-configuration.md` (network trust, credentials); agent settings help | docs |

**Interpretation of "it must require proper tool":** scripts run only through the dedicated script
tools (PowerShell, Python), and every one of them passes the protection pipeline.
- There is no general shell tool, and none is added.
- Requiring an extra capability assignment on top of the setting would break the 14 template
  agents, so it is not introduced.
- Known gaps, recorded and not fixed here:
  - `run_skill_script` (`.sh` via bash, `.js` via node) is gated by skill assignment and does
    not pass through the environment pipeline;
  - dotnet builds can run MSBuild code;
  - local MCP servers.

**Exit:**
- T-B1…T-B12 pass.
- R-B passes.
- UI-B1…UI-B4 are done.
- Portability gate passes.
- Docs check passes.
- INV checks done.

---

## Stage C — Read-only stays read-only (persistent output)

| # | Work item | Main files |
| --- | --- | --- |
| C1 | Plugin decision: when a dotnet restore/build/test/run/publish target (or working directory) is an external alias with `CanRead && !CanWrite` (full access via `fileAccess.ResolveExternalTargetAccess()`), compute the redirect. The key is the first 12 hex characters of SHA-256 of the canonical physical path of the most specific read-only alias root. The location is `<workspace>/.build/<key>` (persistent; INV-1) | `WorkspaceRuntimePlugin.cs`, `WorkspaceValidationToolProvider.cs` |
| C2 | Service and plan: an optional `artifactsPath` parameter (last position) → `--artifacts-path <full>` for restore/build/test/run/run-http/publish | `WorkspaceCommandExecutionService.cs`, `WorkspaceCommandPlanBuilder.cs`, `WorkspacePublishedOutputPlanBuilder.cs` |
| C3 | Message appendix `Build output for this read-only target is kept in '.build/<key>'; the target folder is not modified.`; a receipt target reference | runner/service |
| C4 | Switch `…:RedirectReadOnlyDotnetOutput` (default true) | options |
| C5 | Docs: `runtime-execution-portability.md`, `workspace-published-output-validation.md` if affected | docs |

**Exit:**
- T-C1…T-C6 pass.
- R-C passes.
- UI-C1 and **UI-C2 (the full delivery process)** are done.
- Portability gate passes.

---

## Stage V — Linux, macOS, final regression

- Linux (Docker, local): procedure L-1…L-3 in 07.
- macOS: checklist M-1 in 07 (the operator runs CI and shares the log zip).
- Final: whole Unit assembly, regression lanes R-A/R-B/R-C, portability gate, docs check, memory,
  and a report.
