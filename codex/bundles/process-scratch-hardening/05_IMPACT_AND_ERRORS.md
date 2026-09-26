# 05 — Impact, Error Information and Settings Analysis

Second-pass analysis (2026-09-26), requested because consequences, scale, settings and user-facing
error information were unclear. It supersedes the phase order in the first version of
`03_PHASES.md`.

## 1. Error information: the prerequisite nobody asked for but everyone needs

Hardening adds new ways for a launch to be refused, redirected or degraded. Today the platform
cannot explain even the existing ones:

| Surface | Today | Evidence |
| --- | --- | --- |
| Process host | Every start failure (missing exe, bad working folder, path too long, access denied) becomes one sentence: "The configured workspace process could not be started." On the agent path the cause is **neither logged nor typed**. | `LocalWorkspaceProcessHost.cs:62-76, 161-172`; `WorkspaceProcessContracts.cs:119-127` (`StartFailed` only) |
| Agent tool result | Free text `Recipe '…' failed to start: …`, no failure code, `canRetry=false`, classified with an empty code | `WorkspaceCommandProcessRunner.cs:190-198`; `MafRuntimeToolInvocationResultClassifier.cs:47-69` |
| Chat transcript | No tool-failure card. The user learns the reason only if the model repeats it. | `ChatWorkspaceSurface.razor:24-131` |
| Process runs | Good pattern (`SafeSummary`, `NextAction`) but only for `process.adapter.*`; `process.runtime.*` host-capability failures get a fixed "retry after configuration is ready" | `ProcessRuntimeOperatorDiagnosticDetailsBuilder.cs:33-47`; `ProcessStrategyDispatcher.cs:252-294` |
| Runtime nodes | Feedback banner and quick-action card show a message; the richer `Guidance` goes only to agents | `ProjectStructurePage.RuntimeLaunch.cs:51-53`; `ProjectStructureNodeActionCapabilityResolver.cs` |
| Startup | Readiness failure **stops the host**; the reason is in the console or log only. There is no degraded severity. `/settings/runtime-capabilities` exists with remediation cards but can't be reached when startup fails | `HostCapabilityRuntime.cs:80-106`; `RuntimeCapabilities.razor` |
| Environment | Variables are **silently** dropped; nothing ever says "HTTP_PROXY was not passed" | `WorkspaceCommandEnvironmentPolicy.cs:151-188` |
| Language | English only, with no localization infrastructure (no resx, no `IStringLocalizer`) | agent map §7 |

**Reusable building blocks already exist**: the typed agent failure contract (`IAgentToolFailure`
with code, safe message, `canRetry` and effect state), pre-launch denials with no-effect evidence
(`CreateDeniedResult`), host capability descriptors with remediation, and process diagnostics with
`NextAction`.

**Consequence for the plan:** a small **launch-explanation contract** becomes stage A. It has
value on its own: the Tetris3 runtime-node failure would have been self-explanatory with it.

### Launch-explanation contract (stage A)

| Reason code | Detected from | Operator text (with path) | Agent text (no physical path) | Retry |
| --- | --- | --- | --- | --- |
| `ExecutableNotFound` | locator miss or Win32 2 | "`dotnet` was not found on PATH for the CanDoItAll host. Install it or add it to PATH, then restart CanDoItAll." | same, without PATH contents | no |
| `WorkingDirectoryMissing` | pre-check or Win32 267 | "Working folder … does not exist." | "The working folder does not exist; correct the path." | yes (input) |
| `WorkingDirectoryTooLong` | pre-check (Windows, > 258) | "Working folder path is N characters; Windows limits it to 258 while long paths are disabled. Move the project to a shorter folder or shorten the workspace root." | same meaning, no path | no |
| `AccessDenied` | Win32 5 or `UnauthorizedAccessException` | "The CanDoItAll account cannot run or open …" | generic | no |
| `EnvironmentIncomplete` | composition diagnostics | "TEMP/HOME were missing on the host; CanDoItAll supplied … ." (information only) | none | — |
| `NetworkTrustNotPassed` | host has proxy or CA variables, `NetworkTrust` off | "The host has proxy/CA settings that were not passed to this process. If it needs network access through the proxy, enable `ProcessEnvironment:NetworkTrust`." (a hint in the receipt and in operator views after a failed run) | "Network settings were not passed to the process." | no |
| `TargetRedirected` (stage C) | dotnet on a read-only target | "Built without writing into the read-only target; output is in the run's temporary folder and is deleted when the run ends." | same, with the logical location | — |
| `ScratchDegraded` (stage D) | scratch readiness | "Process temp folders are unavailable (reason); processes use the system temp folder. (remediation)" | none | — |

Where each surface shows it:
- **Agents:** the code and safe text through `IAgentToolFailure`.
- **Receipts:** the operator text.
- **Runtime-node feedback and quick-action card:** the operator text.
- **Process diagnostics:** a new `process.runtime.launch.*` code with a `NextAction` row.
- **Chat execution stream:** the failed tool step shows the code plus the one-line agent text.
  This is a minimal addition, not a new card system.
- **`/settings/runtime-capabilities` and `/api/runtime/capabilities`:** scratch readiness and the
  Windows long-path state.

Messages stay English. Localization is a separate, platform-wide decision (out of scope).

## 2. Settings: how much configuration the hardening adds

| Setting | Level | Default | Needed by | Cost |
| --- | --- | --- | --- | --- |
| "Agent file tools may read this run's temporary folder" (D8) | **Per agent**, in the Workspace tools section of the agent dialog | off | stage D | About 12 source files and 8 test files, **no DB migration, no package format change**. Follows `CanReadStorage` end to end. Traps: add the key to `IsDefault` (or saves silently drop it), to the seed normalizer equality, and to the profile override in `RuntimeCapabilityComposer`. The 31 template `settings.json` files are optional (a missing key reads as false) |
| `ProcessEnvironment:NetworkTrust` (D4) | Host configuration | off | stage B | Options plus a startup validator. Shown on the runtime-capabilities page |
| `ProcessEnvironment:AdditionalInheritedNames` (D4) | Host configuration | empty | stage B | Same options class; names are checked against a denylist |
| `ProcessEnvironment:ManagedScratch` kill switch | Host configuration | `Enabled` after validation | stage D | One option; `Disabled` means today's behaviour (inherited temp) |
| `ProcessEnvironment:ReadOnlyTargetDotnetRedirect` kill switch | Host configuration | `Enabled` | stage C | One option |
| `ControlPlane:ProcessScratchRootPath` (D1) | Host configuration | per-OS default | stage D | Like the other purpose-root overrides |

**Only one new per-agent setting.** Everything else is automatic or host level on purpose.
Per-agent knobs multiply the combinations that have to be tested, and they multiply the ways a
user can be surprised. Process steps can only narrow agent authority, never widen it
(`AgentToolInvocationPolicy.cs:505-511`), so D8 needs no step-level setting.

## 3. Decision impact sheets

Each sheet covers: what changes for users, what can newly fail and how it is explained, size, and
retest.

### D1 Where temp folders live (stage D)
- **Users get:** build and app temp files in `…\Temp\CanDoItAll\scratch\<run>` (Windows),
  `/tmp/candoitall-<hash>/<run>` (Linux) or `/private/tmp/candoitall-<hash>/<run>` (macOS),
  deleted when the run ends.
- **New failure:** the root is unusable (squatted on Unix, unwritable, over budget) → `ScratchDegraded`,
  and processes still run with the system temp.
- **Why not reuse `RuntimeTemporary`:** it is already a startup-blocking purpose root, and its
  macOS default is too long. Changing it touches documented service layouts.
- **Size:** policy class, readiness entry, options, docs (4 files); tests for 3 OS as a pure matrix.

### D2 One temp folder per run, node session or MCP session (stage D)
- **Users get:** commands in one agent run share temp (faster repeated builds, consistent
  behaviour).
- **Risk:** a folder is deleted while something still uses it. Mitigation: delete only after the
  process tree is terminated. Reuse the existing per-run process-lease cleanup
  (`WorkspaceExecutionRunProcessLeases`, already called on completion and cancellation), which
  is lower risk than a new lifecycle hook.
- **Size:** allocator plus wiring 9 launchers (M1–M9): the largest part of the work.

### D3 What happens when the temp root is broken (stage D) — **revised**
- The first version proposed "explicit misconfiguration blocks startup". The error-surface map
  shows a blocked startup is visible **only in the console**. So the recommendation now is:
  **never block startup because of temp**. Show `ScratchDegraded` (with remediation) on the
  runtime-capabilities page and API, log an error, and let processes use the system temp.
- An explicit override that fails gets the "Error" styling on that page instead of "Warning".

### D4 Proxy and CA variables (stage B)
- **Users get:** on proxied networks, restore and install inside agent processes can work after
  one host setting. Today they fail, unexplained.
- **New information:** the `NetworkTrustNotPassed` hint when the host has proxy variables that
  were not passed.
- **Risk:** proxy URLs may carry credentials. That is why the group is opt-in and documented.

### D5 dotnet on read-only targets (stage C)
- **Users get:** building, testing or running a product the agent may only *read* no longer
  writes `bin`/`obj` into it. The output lives in the run's temp folder.
- **What can surprise:**
  - A cold build per run (no incremental `obj`).
  - Tools or prompts that expect `bin/Debug/...` next to the project.
  - Projects that already set `UseArtifactsOutput`.
- **Explained by:** `TargetRedirected` in the tool result and receipt.
- **Retest:** the full delivery process (build, test, run, publish, static serve against an
  external folder), since it is the heaviest real user.
- **Kill switch:** available.

### D6 Terminal mode gets the same environment rules (stage B)
- **Users get:** a node opened in a terminal no longer receives host secrets
  (`OPENAI_API_KEY` …).
- **What can surprise:** an operator who relied on their own tokens inside a node terminal.
- **Explained by:** a one-line prelude in the terminal ("CanDoItAll passed N allowed variables;
  add names under `ProcessEnvironment:AdditionalInheritedNames`").

### D7 OS containment — **decided: not now**
- No containers or OS sandboxes in this work (the operator decided on 2026-09-26: a larger change,
  it needs Docker, and it has wider consequences). Receipts keep saying "policy only, no host
  isolation".

### D8 Agent file tools and temp folders — **decided: per-agent option**
- Default off. When on, it gives **read-only** access to **the current run's own** temp folder,
  never other runs' folders.
- Explained by a new `WorkspaceAccessDenied` factory: "This agent may not read process temporary
  folders. Enable 'Read run temporary files' in the agent's Workspace tools settings."

## 4. Risk register

| # | Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| R1 | Tools or apps behave differently with a new `TEMP` (build-server reuse, apps keeping state in temp) | Medium | Medium | Per-run sharing; kill switch; the delivery-process regression run |
| R2 | A folder is deleted while an orphan still writes (Unix groups survive a host crash; Windows locked files) | Medium | Low | Delete only after tree termination; the sweep checks PID and start time; retries, then the sweep |
| R3 | **Deletion follows a link out of the temp folder** (data loss) | Low | **Critical** | Reparse-safe delete; dedicated tests on all three OS (V5) before stage D is enabled |
| R4 | macOS cannot be verified locally | Certain | Medium | CI host lane is mandatory; keep macOS on the kill switch `Disabled` until CI proves V4–V6 |
| R5 | Redirected dotnet output breaks expectations of steps or prompts | Medium | Medium | Only for read-only targets (writes there were already illegitimate); `TargetRedirected` message; kill switch |
| R6 | Terminal parity removes variables operators used | Low | Low | Extension list; terminal prelude text |
| R7 | New messages change model behaviour (the agent reacts to new codes) | Medium | Low | Codes follow the existing typed-failure rules; `canRetry` only for input errors; the MafGeneric admission lanes |
| R8 | Test churn is larger than expected | Medium | Medium | About 650 test methods in 28 directly affected classes; only 3 assert start-failure text; stage-by-stage lanes |
| R9 | Approvals pending across the upgrade execute with the new behaviour (plans are built at execution time) | Low | Low | Acceptable; noted in the release notes |
| R10 | Unix `/tmp` is tmpfs (RAM) or `noexec` on hardened hosts | Low | Medium | Budget/readiness check reports `noexec`; override `ControlPlane:ProcessScratchRootPath` |

## 5. Retest scope

| Stage | Automated | Manual / E2E |
| --- | --- | --- |
| A Explainable launches | Unit (whole assembly, about 9,000 cases, about 2 min); Integration: `MafGenericToolAdmission*`, `WorkspaceCommandExecution*`, `ProjectStructureAgent*`, process diagnostics classes; Components: `ProjectStructurePage*`, chat surface | Force each reason on 5032 (missing exe, missing folder, 270-character folder) from a runtime node, an agent chat and a process step |
| B Safe environment | Unit: environment policy matrix, runtime adapters, MCP and Docker portability; Integration: `McpExternalToolPortability*`, `ExecutionFoundationPortability*` | Terminal parity; `NetworkTrust` on and off; a script node printing its environment |
| C Read-only targets | Integration `[HostPlatform]` read-only dotnet; the existing dotnet recipe tests | Agent build on a read-only external target; **the full delivery process on a sample app** |
| D Managed temp | Unit: root policy, allocator, sweeper; Integration `[HostPlatform]` on Windows, Linux (Docker) and macOS (CI): privacy, links, named pipes, sweep; agent-settings round trip, seed and UI tests for D8 | Journeys J1–J10; crash and restart sweep; D8 toggle |
| Closure | The whole Unit assembly; the portability gate; the docs check; **one CI run with `platform-scope: full`** (Windows/macOS host lanes, 85–275 minutes per OS) | — |

## 6. Size (relative, with anchors)

| Stage | Source files | New tests | Relative size |
| --- | --- | --- | --- |
| A Explainable launches | about 15–20 | about 30–40 | M |
| B Safe environment | about 10–12 | about 25 | M |
| C Read-only targets | about 5–6 | about 8 | S |
| D Managed temp + D8 | about 25–30 (9 launchers, 12 for the D8 setting) | about 40–50 | L |
| Validation and closure | — | — | M, plus CI wall time |
