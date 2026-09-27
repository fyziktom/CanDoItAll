# 01 — Architecture Analysis

Snapshot of `development` at `8faef0000` plus the uncommitted runtime-node repairs from
2026-09-26. Line numbers are from that snapshot. Measured facts are in
[evidence/PROBE-RESULTS.md](evidence/PROBE-RESULTS.md).

## 1. How a child process gets its environment today

Every managed launch goes through `LocalWorkspaceProcessHost`
(`src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`).
`BuildStartInfo` (:387-413) calls `startInfo.Environment.Clear()` and then copies exactly the
dictionary the caller passes. A `null` value becomes an empty string, not a missing variable
(:402-405). The host adds nothing, so every caller must compose the environment first.

Callers compose through `WorkspaceCommandEnvironmentPolicy.MergeEnvironmentVariables(env, toolName)`
(`…/Workspace/Commands/WorkspaceCommandEnvironmentPolicy.cs`):

- **Common allowlist** (all OS): `HOME LANG LC_* PATH TEMP TMP TMPDIR TZ`. On Windows it adds
  `APPDATA LOCALAPPDATA USERPROFILE SystemRoot …`. On Unix it adds `LOGNAME SHELL USER`.
- **Tool lists** keyed by tool-name prefix: `workspace_dotnet_*`, `workspace_python_run_file`,
  `workspace_pwsh_run_script`, `docker`.
- **Explicit values** overlay everything, and can add any name.
- **Excluded on purpose**, with tests (`WorkspaceCommandEnvironmentPolicyTests`): `MSBUILD*`,
  `DOTNET_STARTUP_HOOKS`, `DOTNET_WATCH*`, `OPENAI_API_KEY`, and `PIP_INDEX_URL` (which can carry
  credentials).

So temp, home and caches are **pass-through from the host process**. Nothing in `src` sets
`TEMP`/`TMP`/`TMPDIR`, home or cache variables for a child.

## 2. Launch inventory

Managed launches go through `LocalWorkspaceProcessHost`, which uses a Windows job object with
kill-on-close, or a Unix `setsid` process group.

| # | Launcher | Initiated by | Environment profile | Path-length handling |
| --- | --- | --- | --- | --- |
| M1 | `WorkspaceCommandProcessRunner.ExecuteAsync` | agent tools (git, dotnet restore/build/test/new/run, python, pwsh, publish, skill scripts, spreadsheet) and process steps (managed script, `dotnet new`) | by tool name | Windows `subst` alias (M2) |
| M2 | `WorkspacePathAliasSession` (`subst`) | M1 | M1's | Windows only; activates when the root is ≥ 120 or a path is ≥ 240 |
| M3 | `WorkspaceDotnetProcessLifecycle.RunAsync` | agent `dotnet run` with HTTP wait, `workspace_static_serve` | `workspace_dotnet_run` / **static serve: common only** | none |
| M4 | `WorkspaceGitCommandExecutor` | process adapter audit (no live caller found) | common only | none |
| M5 | `WorkspaceExternalProcessRunner` | operator capability setup test | common only | none |
| M6 | `PlaywrightMcpLaunchResolver` npm install | MCP launch preparation | common only | none |
| M7 | `LocalStdioMcpProcessLauncher` | agent MCP composition, operator MCP setup test | common only (`local_mcp`) | none |
| M8 | `ProjectStructureRuntimeSessionRegistry` via the Direct adapter | operator **Run** on a runtime node | by node kind; **Direct/POSIX: common only** | none |
| M9 | `DockerHostToolService` | Docker workflow executors | `docker` | none |

Unmanaged launches use `Process.Start` directly and are not tracked:

| # | Launcher | Environment |
| --- | --- | --- |
| D1 | `ProjectStructureTerminalPresenter` (runtime node **Terminal**) | **full host environment** (Windows `UseShellExecute=true`) |
| D2 | `ProjectStructureRuntimeElevationAdapter` (`runas`) | full host environment; plans with variables are refused |
| D3 | `LinuxSecretServiceCommandRunner` (`secret-tool`) | full host environment (needs D-Bus) |
| D4 | FileTools desktop open | full host environment |

## 3. What the platform already enforces

Every check happens **before** launch. After launch, a child has the host user's full rights.

| Control | What it does | Where |
| --- | --- | --- |
| Path authority | Workspace containment, external-target aliases (read vs write), no link traversal | `WorkspacePathPolicy`, `WorkspaceRuntimeFileAccessGuard`, `EffectiveExternalTargetAccessScope`, `ExternalTargetPathRegistry` |
| Tool policy and approval | Mutation tools need approval, Validation/Read tools do not | `AgentToolInvocationPolicy`, `ToolCapabilityRegistry` |
| Script inspection | Governed runs require an inspected script and a side-effect manifest | `ProcessWorkspaceInvocationPolicy`, `WorkspaceScriptSideEffectAnalyzer` |
| Post-execution audit | Hashes product-target files before and after; changes turn exit 0 into failure. **Covers pwsh and python only** | `ProductTargetMutationAudit` (`WorkspaceCommandProcessRunner.cs:341-731`) |
| Lifetime | Job object / process group, kill tree, timeouts | `LocalWorkspaceProcessOwnership` |
| Honest labelling | Boundary `PolicyOnlyLocal`, `host-enforced: false`; the UI shows "Policy only; no host isolation" | `LocalWorkspaceProcessHost.cs:29-36`, `DiagnosticsPresentation.cs:42-47` |

There is no AppContainer, restricted token, namespace, chroot or container around a child.

## 4. Findings

Severity is **H**igh, **M**edium or **L**ow. "Proven" means reproduced in this session.

| ID | Sev | Finding | Evidence |
| --- | --- | --- | --- |
| F1 | H | **No managed temp.** Every managed child uses the host user's shared temp. Build, test, app and script temp files pile up there with no owner, no attribution and no cleanup, and runs can see each other's leftovers. | policy allowlist; §1 |
| F2 | H | **Missing values degrade badly.** With no `TMP`/`TEMP`/`USERPROFILE`, Windows resolves temp to the Windows directory and `dotnet run` fails with MSB1025. A `null` explicit value becomes `""`, which is worse than absent. A Linux service without `HOME` breaks the dotnet CLI and NuGet first-run folders. | Proven (Tetris3 node); `LocalWorkspaceProcessHost.cs:402-405`; the HOME case is known platform behaviour, verify in P6 |
| F3 | M | **The profile follows the tool name, not the executable.** `workspace_static_serve` runs `dotnet` but gets no dotnet variables. `skill_script_run` (py/ps1/sh/js), `workspace_inspect_spreadsheet` (python) and Direct/POSIX runtime nodes get the common list only. There is no node/npm profile. The same executable behaves differently depending on which launcher starts it. | `WorkspaceCommandEnvironmentPolicy.cs:204-232`; agent map |
| F4 | M | **The `RuntimeTemporary` purpose root is unused and its defaults don't fit scratch.** macOS `…/T/CanDoItAll/runtime` is about 74 characters, over the socket budget once a per-run folder is added. The Linux fallback `/tmp/candoitall-runtime` is a fixed shared name that another local user can pre-create; the 0700 check prevents takeover but blocks startup. The Linux default `$XDG_RUNTIME_DIR` is a tmpfs meant for small files. | `ApplicationPurposeRootPolicy.cs:94-182`; only `PathFoundationReadiness.cs:150` uses it |
| F5 | H | **Unix socket budget.** .NET named pipes are sockets at `$TMPDIR/CoreFxPipe_<name>`, limited to 108 bytes on Linux and 104 on macOS. A long `TMPDIR` crashes `dotnet watch` hot reload, compiler and Razor build servers, and IPC in user apps. | Proven: `ArgumentOutOfRangeException` at `TMPDIR` length 95 |
| F6 | M | **Windows path budget.** Long paths are disabled by default (and on this machine). A working directory over about 258 characters cannot start any process, and a `TEMP` of about 245 breaks `dotnet build`. Only M1 has the `subst` alias; M3, M5, M7 and M8 fail with a generic "could not be started". | Proven; `WorkspacePathAliasSession.cs:5-6` |
| F7 | L | **`subst` has no crash recovery.** A drive letter mapped when the host dies stays mapped for the logon session, and stale letters use up P–Z over time. | agent map; no sweep exists |
| F8 | M | **Terminal presentation leaks the host environment.** Running a runtime node in Terminal mode hands the whole host environment, including `OPENAI_API_KEY`, to code that an agent may have written. Direct mode strips it. The two modes are inconsistent. | `ProjectStructureRuntimeAdapters.cs:295-399` |
| F9 | — | **No OS confinement.** A child can write anywhere the user can. This is stated honestly today (`host-enforced: false`) and must stay honest. | §3 |
| F10 | H | **"Read-only" external targets are written by builds.** dotnet restore, build, test, run and publish need only *read* access (`WorkspaceRuntimePlugin.cs:405-413`) and are classed Validation (no approval), yet they write `bin`/`obj` into the target. The product audit does not cover dotnet. `--artifacts-path` keeps the target untouched. | Proven by probe |
| F11 | L | **Receipts contradict classification.** The plan marks `workspace_dotnet_restore` as `ApprovalRequired=true` in the receipt, but the registry classes it Validation, so no approval happens. | agent map §3 |
| F12 | M | **Process groups can outlive the host.** Unix groups survive a host crash (no parent-death link). On Windows the job is assigned after `Process.Start`. Cleanup has to tolerate orphans still writing, and a sweep must check owner liveness. | `LocalWorkspaceProcessOwnership.cs` |
| F13 | M | **Network trust variables are stripped.** `HTTP(S)_PROXY`, `NO_PROXY`, `SSL_CERT_FILE/DIR`, `NODE_EXTRA_CA_CERTS` and `REQUESTS_CA_BUNDLE` never reach children. On a proxied or TLS-intercepting network (this workstation intercepts TLS) NuGet, pip and npm inside a child can fail. Proxy URLs can hold credentials, so inheriting them by default is also wrong. | allowlist; environment memory (git needs schannel) |
| F14 | L | Out of scope, noted only: `secret-tool` is not killed on caller cancellation, and desktop open and elevation inherit the full host environment, which is acceptable for operator desktop actions. | agent map D2–D4 |

## 5. Platform facts that shape the design

| Concern | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Temp resolution | `TMP` → `TEMP` → `USERPROFILE` → Windows dir | `TMPDIR` → `/tmp/` | `TMPDIR` (per-user `/var/folders/…/T/`, about 49 characters) |
| Hard path limit that bites | 260 (working directory about 258; `TEMP` ≤ about 200 for builds) | socket path 108 | socket path 104; `TMPDIR` is already near it |
| Safe scratch-leaf budget | about 110 characters | about 47 characters | about 47 characters |
| Shared-temp risk | per-user `%LOCALAPPDATA%\Temp` with ACLs | `/tmp` is shared and sticky: pre-creation, symlink planting; may be tmpfs (RAM), may be `noexec`, cleaned by tmpfiles after about 10 days | `/private/tmp` is shared; `/tmp` and `/var` are symlinks; periodic cleanup |
| Private-folder primitive | ACL inheritance from the user profile | mode 0700, verified; chmod succeeds only for the owner | same as Linux |
| Tree ownership | job object, kill-on-close | `setsid` group, no parent-death link | `setsid` via perl, no parent-death link |
| Real confinement available | AppContainer/LPAC, restricted token and low integrity, separate account (all break toolchains or need admin setup) | bubblewrap, landlock (kernel ≥ 5.13), user namespaces (Ubuntu 24.04 restricts them), containers | `sandbox-exec` (deprecated), containers |

## 6. Threat model and the balance

**What we are protecting against, in order of likelihood:**

1. **Accidental damage by agent-driven processes.** Writing into product trees that were granted
   read-only, filling disks, orphaned processes, cross-run contamination through shared temp,
   and host secrets reaching agent-written code.
2. **Untrusted code**, from prompt injection or a supply-chain dependency. Without OS confinement
   this cannot be *prevented*, only made less likely (least environment, approvals, audits) and
   *visible* (receipts). Real prevention needs contained execution, which is out of scope here
   (decision D7).

**Principles that set the balance:**

- **Fail closed on authority, fail soft on hygiene.** *Who may target what* stays strict. *Where
  temp goes* must never stop a legitimate run. If managed scratch is unavailable, the launch uses
  the host temp and the receipt says why. It never falls back to the Windows directory and never
  to an empty value.
- **Explicit configuration is validated strictly; defaults degrade gracefully.** A root the
  operator configured that is invalid blocks startup with a clear message. A default root that
  is unusable degrades.
- **Budgets are checked at startup, not discovered by failing builds.** Paths are short,
  deterministic and aware of the OS.
- **Shared caches stay shared.** NuGet packages, pip, npm and Playwright browsers are
  content-addressed caches. Isolating them costs minutes per run and breaks offline work, so
  that would be too strict.
- **One owner composes the environment.** Launchers declare intent (owner, executable kind,
  explicit values), not raw variables.
- **Operator extensions are names only, explicit and checked against a denylist.** They are never
  on by default.
- **Receipts stay honest.** They keep saying `policy-only`, and add where scratch was and whether
  the command could write outside the workspace.
