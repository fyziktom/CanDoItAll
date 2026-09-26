# 02 — Target Design

The design has three tiers. **T1** and **T2** are this bundle. **T3** is not planned now (D7,
decided 2026-09-26). Stage **A** (explainable launches, [05 §1](05_IMPACT_AND_ERRORS.md)) is a
prerequisite for both tiers.

| Tier | Goal | Kind of guarantee |
| --- | --- | --- |
| T1 Managed process environment | Every managed child gets a predictable, minimal environment with its own short, private, cleaned-up scratch folder | Predictability and hygiene. **Not** confinement |
| T2 Write-scope tightening | Read-only targets stay untouched by builds; host secrets never reach agent-written code in any launch mode; receipts tell the truth | Removes the known write and secret gaps without OS sandboxing |
| T3 Contained execution (follow-up) | Opt-in container execution for untrusted steps and code | Real OS boundary, the same on all three platforms |

## 1. Components

### 1.1 `ProcessScratchRootPolicy` (Infrastructure, next to `ApplicationPurposeRootPolicy`)

This is a pure function of `ApplicationRootEnvironment`, so all three platforms can be unit-tested
on any host. It returns the **process scratch root** and the budget it was checked against.

| OS | Default root | Example length | Why |
| --- | --- | --- | --- |
| Windows | `%TEMP%\CanDoItAll\scratch` | 46 | Per-user ACL'd temp, short enough |
| Linux | `/tmp/candoitall-<u8>` | 24 | Literal `/tmp`, **not** `$TMPDIR`, so a long `TMPDIR` cannot break the socket budget. Disk-backed on most distributions, unlike `$XDG_RUNTIME_DIR` |
| macOS | `/private/tmp/candoitall-<u8>` | 32 | The default `$TMPDIR` is already about 49 characters. `/private` form because managed roots reject links |

`<u8>` is the first 8 hex characters of SHA-256 of the user name. It is stable, carries no user
name, and keeps the name fixed-length. On Unix the root is created and verified at mode 0700
through `DurableFileWriter.EnsureDirectory(requirePrivateUnixMode: true)`. Because chmod succeeds
only for the owner, that is also the ownership check. Link traversal is rejected with the existing
`PhysicalFileSystemPathPolicy.EnsureSafePath`.

**Override:** `ControlPlane:ProcessScratchRootPath`, expanded like the other purpose roots.

**Budgets** apply to the root, so the per-owner leaf (`/` plus 10 characters) still fits:

- Windows: root ≤ 96 characters, leaving about 150 for tool internals (a measured `TEMP` of 205
  still built).
- Unix: root ≤ 36 characters, because 36 + 11 + `/CoreFxPipe_` (12) + a 45-character pipe name
  = 104.

### 1.2 Scratch readiness (never blocks startup; revised 2026-09-26)

`PathFoundationReadiness` has no severity today, so every failure blocks startup. A blocked
startup is visible **only in the console** (05 §1). Process scratch is therefore reported as an
**optional host capability**. It shows on `/settings/runtime-capabilities` and
`/api/runtime/capabilities` with its reason code and remediation:

| Situation | Result | Effect |
| --- | --- | --- |
| Root usable and within budget | `Managed` | Launches get managed scratch |
| **Configured** override invalid (missing parent, over budget, link, mode or ownership, `noexec`) | `Degraded`, shown as an **error**, plus an error log | Launches use `InheritedHostTemp`, and receipts record the reason |
| **Default** root unusable (squatted, unwritable, over budget) | `Degraded`, shown as a **warning** | Same |
| Switch-off `ProcessEnvironment:ManagedScratch = Disabled` (per OS) | `Disabled` | Today's behaviour |

On Windows the host capability view also reports the long-path state (`LongPathsEnabled`). That is
information only and never required.

### 1.3 `IProcessScratchAllocator` (AgentFramework.Core; its root provider lives in Infrastructure.Abstractions)

`AgentFramework.Core` references only `Infrastructure.Abstractions`, so the core owns the
allocator and gets the root through `IProcessScratchRootProvider` (root, mode, reason). The
provider is implemented in Infrastructure.

```text
AcquireAsync(ProcessScratchOwner owner, CancellationToken) -> ProcessScratchLease (IAsyncDisposable)
ProcessScratchOwner = ExecutionRun(execRunId) | RuntimeNode(projectId, nodeId, sessionId)
                    | McpSession(sessionId) | Command(receiptId)
```

- **Leaf name:** `<k>-<id8>`, where `k` is one of `e/n/m/c` and `id8` is 8 hex characters of a
  fresh GUID. It is always 10 characters. The leaf is created exclusively, with a new name on
  collision.
- **Marker:** `.cdia-scratch.json` holds `{ ownerKind, ownerKey, hostInstanceId, hostPid,
  hostProcessStartUtc, createdUtc }`.
- **Sharing:** leases are reference-counted per owner. All commands of one execution run share
  one scratch (NuGet scratch is reused and the agent sees one consistent temp). A lease is
  released when its owner ends.
- **Release:** after the process tree has been terminated, delete with a **reparse-safe**
  recursive delete. It never follows a junction or symlink; it removes the link itself. Windows
  gets 3 retries with backoff for files that are still locked. Anything left over goes to the
  sweeper.
- **Degraded mode:** returns a lease with `Mode = InheritedHostTemp` and `Path = null`. Callers
  then keep the inherited `TEMP`/`TMP`/`TMPDIR` and never set them empty.

### 1.4 `ProcessScratchSweeper` (hosted service)

It runs at startup and then every 30 minutes. For each leaf under the root it deletes the leaf
when any of these holds:

- the marker's host process is gone (PID + start time do not match a live process);
- the marker is missing and the leaf is older than 1 hour;
- the marker belongs to this host, but no live lease matches it and it is older than 24 hours.
  This case also logs a leak warning.

It logs a deletion summary: count and bytes.

On Windows it also removes stale `subst` mappings (F7). That means letters P–Z whose target is
**exactly** the current workspace root, when no alias session is live.

### 1.5 Environment composition (one owner)

Extend `WorkspaceCommandEnvironmentPolicy` with an intent-based entry point. The old
`MergeEnvironmentVariables(env, toolName)` remains as a thin adapter until every launcher has moved.

```text
Compose(ProcessEnvironmentRequest {
    Toolchain,            // derived from the resolved executable, not the tool name
    ExplicitVariables,
    Scratch,              // ProcessScratchLease
}) -> ProcessEnvironment { Variables, Diagnostics }
```

The rules, in order:

1. **Base allowlist:** unchanged.
2. **Toolchain profile by executable:** `dotnet` → dotnet list; `python`/`py`/`conda` → python;
   `pwsh`/`powershell` → pwsh; `node`/`npm`/`npx` → node (new list); `git` → common;
   `docker` → docker; otherwise common. This fixes static serve, skill scripts, the spreadsheet
   tool and Direct runtime nodes (F3).
3. **Operator extension:** `AgentFramework:ProcessEnvironment:AdditionalInheritedNames`, names
   only. It also offers a curated **`NetworkTrust`** group (`HTTP_PROXY HTTPS_PROXY NO_PROXY
   ALL_PROXY SSL_CERT_FILE SSL_CERT_DIR NODE_EXTRA_CA_CERTS REQUESTS_CA_BUNDLE`), off by default.
   - Names on the denylist are rejected at startup: known instrumentation (`DOTNET_STARTUP_HOOKS`,
     `MSBUILD*`, `DOTNET_WATCH*`, `COR_*`, `CORECLR_*`, `NODE_OPTIONS`, `LD_PRELOAD`,
     `DYLD_*`) and any name containing `KEY`, `TOKEN`, `SECRET`, `PASSWORD` or `CREDENTIAL`
     (F13).
4. **Scratch:** set `TEMP`, `TMP` **and** `TMPDIR` to the lease path on every OS. Setting all
   three is predictable, because Python checks `TMPDIR`, `TEMP` and `TMP` in that order.
5. **Home guarantee (Unix):** if `HOME` is missing, set `HOME` and `DOTNET_CLI_HOME` to
   `<StateRoot>/toolchain-home` (mode 0700) and record it in diagnostics (F2).
6. **Explicit values** overlay last, as today.
7. **Never empty:** `null` means "do not set". An empty `TEMP`/`TMP`/`TMPDIR`/`HOME` is dropped
   and recorded (F2).

`Diagnostics` holds the toolchain, the scratch mode and reason, the inherited names, the added
names and any dropped empty names. It carries **names only, never values**.

### 1.6 Launch-mode parity (F8)

The terminal presentation builds its environment through the same `Compose`, with the inherited
host temp and no scratch lease, because a terminal is not tracked.

- **Unix:** `env -i K=V … exe args`.
- **Windows:** the `-Command` prelude removes every environment variable that is not in the
  composed set before it sets the node's values.

Elevation (`runas`) keeps refusing plans that carry variables, since ShellExecute cannot pass an
environment. Desktop open stays as it is (operator desktop semantics).

### 1.7 Read-only targets and dotnet (F10)

When a `workspace_dotnet_{restore,build,test,run,publish}` target resolves under a **read-only**
alias, or under a product target whose step disallows product mutation, the plan adds
`--artifacts-path <scratch>/dotnet` (`publish` keeps its managed `--output`).

- The product tree stays untouched, as proven by the probe.
- A `dotnet run` with keep-alive holds its execution-run lease until the lifecycle cleanup
  terminates it.
- The receipt records `artifactsRoot`. Agent-facing messages say where `bin` is.
- **Writable** targets are unchanged, so developers keep incremental builds.
- If the project already sets `UseArtifactsOutput` or a custom `BaseIntermediateOutputPath`,
  the redirect is still applied, and a conflict surfaces as a normal build error. That is
  covered by a test.

The post-execution product audit is **not** extended to dotnet. Hashing large repositories
would hit the audit limits (2048 files or 100 MiB). Those limits deny the command, which would be
too strict.

### 1.8 Honest receipts and UI

- The receipt `environment` block gets: `toolchain`, `scratch: { mode, path, reason }`,
  `artifactsRoot?`, `inheritedNames`, `addedNames`.
- `boundary` stays `PolicyOnlyLocal (host-enforced: false)`.
- Fix F11: the receipt approval field reflects the effective registry classification.
- Diagnostics UI and the readiness API show scratch readiness (mode, root, budget, reason) and
  the Windows long-path state.
- Operator guidance for a runtime node shows its scratch path and the last exit.

### 1.9 Path budget for working directories (F6)

M3, M5, M7 and M8 check the working-directory length on Windows before launch. They fail with the
typed reason `WorkingDirectoryTooLong` and the guidance "move the target or shorten the workspace
root". The error is no longer a generic "could not be started". M1 keeps its `subst` alias.

## 2. Lifecycle hooks (owners and release points)

| Owner | Acquire | Release |
| --- | --- | --- |
| ExecutionRun | First managed command of the run (M1, M3, M9 inside a run) | Through the existing `WorkspaceExecutionRunProcessLeases` cleanup (already called on completion and on cancellation by `AgentFrameworkProcessRuntimeCancellationObserver`), after keep-alive processes are terminated |
| RuntimeNode | `ProjectStructureRuntimeSessionRegistry.StartSessionAsync` | Stop, observed exit, or host shutdown |
| McpSession | `LocalStdioMcpProcessSession` start (and M6 npm install for that launch) | Session dispose or stop |
| Command | M1/M5 with no execution run (operator setup test, adapter audit) | Command end |

## 3. Decisions (to confirm before the phase that needs them)

Stages are defined in [03_PHASES.md](03_PHASES.md). Impact sheets for each decision are in
[05_IMPACT_AND_ERRORS.md §3](05_IMPACT_AND_ERRORS.md#3-decision-impact-sheets).

| ID | Decision | Recommended / decided | Alternative | Needed by |
| --- | --- | --- | --- | --- |
| D1 | Where scratch lives | New **process scratch root** with short per-OS defaults (§1.1) | Reuse `RuntimeTemporary` and change its defaults (it is startup-blocking, and it affects documented service layouts) | Stage D |
| D2 | Granularity | Per execution run (reusing the existing run process-lease cleanup), per runtime-node session, per MCP session, per ad-hoc command | Per command always (loses NuGet scratch reuse; the agent sees a different temp per call) | Stage D |
| D3 | Failure policy | **Never block startup**: degraded with inherited host temp, shown on the capabilities page and API (revised) | Block startup on an explicit override (console-only error) | Stage D |
| D4 | Proxy and CA variables | Opt-in `NetworkTrust` group plus a names-only extension list with a denylist; `NetworkTrustNotPassed` hint | Inherit by default (leaks proxy credentials) or never (breaks proxied networks silently) | Stage B |
| D5 | dotnet on read-only targets | Automatic `--artifacts-path`, with the `TargetRedirected` explanation and a switch-off | Report only, or deny builds on read-only targets | Stage C |
| D6 | Terminal presentation | Compose the environment through the policy, with a one-line terminal explanation | Leave the full host environment (secret leak) | Stage B |
| D7 | OS confinement | **Decided 2026-09-26: not now.** A larger change, needs Docker, wider consequences | — | — |
| D8 | Agent file tools and scratch | **Decided 2026-09-26: a per-agent option** "Read run temporary files", default off, read-only, own run's leaf only, not a global rule | — | Stage D |

## 4. Non-goals

- Changing shared tool caches (NuGet packages, pip, npm, Playwright browsers).
- Quotas or disk-size enforcement. Size is measured and logged only.
- Enabling Windows long paths or changing the operator's git or registry configuration.
- The `secret-tool` cancellation behaviour, desktop open, and the elevation environment (F14).
