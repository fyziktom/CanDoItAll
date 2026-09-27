# Manager Summary: Process Environment Hardening

*2026-09-26. Details: [05_IMPACT_AND_ERRORS.md](05_IMPACT_AND_ERRORS.md) (impact, risks, retest),
[01_ANALYSIS.md](01_ANALYSIS.md) (facts), [evidence/](evidence/PROBE-RESULTS.md) (measurements).*

## In one paragraph

Programs that CanDoItAll starts for agents, processes and runtime nodes take their temp folder,
home and toolchain settings from whatever the CanDoItAll host happens to have. When something is
missing, they fail in confusing ways: the Tetris3 node failed because temp resolved to
`C:\WINDOWS`. When something is too long, they break at hard OS limits (measured on this
machine and on Linux). And when they fail, users mostly see one generic sentence. Two real safety
gaps exist:
- **builds write into project folders the agent may only read**;
- **Terminal mode hands host secrets to agent-written code**.

The proposal fixes these in four independently shippable stages. The first stage makes every
launch failure explain itself, and every stage has a switch-off.

## What is wrong today

1. **Failures are opaque.** Any start problem becomes "The configured workspace process could not
   be started." On the agent path the real cause is not even logged. The chat shows no failure
   reason, and startup problems appear only in the console.
2. **Missing or long values break tools.**
   - No `TEMP` means Windows uses its own folder (Tetris3).
   - Windows cannot start a program in a folder longer than about 258 characters.
   - On Linux and macOS a temp path longer than about 60 characters crashes .NET inter-process
     communication (hot reload, build servers).
3. **Read-only is not read-only for builds.** An agent allowed only to *read* a product folder can
   still build, test and run it, which writes `bin`/`obj` into it without approval.
4. **Terminal mode leaks secrets.** A runtime node opened in a terminal gets every host
   variable, including `OPENAI_API_KEY`. Direct mode already strips them.
5. **No cleanup or attribution.** Build, test and app temp files pile up in your personal temp
   folder with no owner.

**Not a goal:** real OS isolation. Containers were decided against for now (larger change, needs
Docker). The product keeps stating honestly that processes are governed by policy, not by an OS
sandbox.

## Proposal: four stages, in this order

| Stage | What users get | Risk | Size | Switch-off |
| --- | --- | --- | --- | --- |
| **A. Explainable launches** | Every blocked or failed launch says *why* and *what to do*, to the operator (runtime node, process run, capability page, receipt) and to the agent (typed code, so it stops guessing) | Low | M | — (text only) |
| **B. Safe environment** | Programs always get a sane temp and home; the right toolchain settings for what actually runs; Terminal mode stops leaking secrets; optional proxy/certificate pass-through for corporate networks | Low–Medium | M | — (security fix; one opt-in setting) |
| **C. Read-only stays read-only** | Building, testing or running a read-only product no longer writes into it (proven by measurement); output goes to the run's temp folder | Medium | S | Yes |
| **D. Managed temp folders** | Each agent run, runtime node and MCP session gets its own short, private temp folder, deleted when it ends; crash leftovers are swept; per-agent option to let an agent read its own run's temp files | Medium–High (most moving parts) | L | Yes (per OS; macOS stays off until CI proves it) |

**Recommendation:** approve **A → B → C** now; they need only two small decisions. Decide the
details of **D** after A–C are validated. By then the new error information will show how often
temp problems actually occur.

## What users will see (examples)

| Situation | Before | After |
| --- | --- | --- |
| Project folder path too long | "The configured workspace process could not be started." | "The working folder path is 271 characters; Windows limits it to 258 while long paths are disabled. Move the project to a shorter folder or shorten the workspace root." |
| Proxy network, NuGet restore fails | Restore error only | Plus: "The host has proxy settings that were not passed to this process. If it needs network access through the proxy, enable *Process environment → Network trust*." |
| Agent builds a read-only product | Silently writes `bin`/`obj` into it | "Built without writing into the read-only target; output is in the run's temporary folder and is deleted when the run ends." |
| Temp root unusable (for example taken by another account on Linux) | *(new situation)* | The runtime capabilities page shows "Process temp folders: Degraded — reason — fix". Programs keep working with the system temp. **The app is never blocked from starting.** |
| Agent tries to read temp files | *(new situation)* | "This agent may not read process temporary folders. Enable *Read run temporary files* in the agent's Workspace tools settings." |

Messages are English only. The platform has no localization infrastructure; that would be a
separate, platform-wide decision.

## Settings impact

- **One new per-agent setting** (stage D): "Read run temporary files", off by default. It follows
  an existing setting's pattern, with no database migration and no package format change.
- **Host settings** (operator level, not per agent): network-trust opt-in, an extra-variable list
  (names only, with a denylist for secret-like names), two switch-offs (C, D) and an optional
  temp-root override.
- We deliberately avoid more per-agent options. Each one multiplies the combinations to test and
  the ways a user can be surprised.

## Top risks

| Risk | Mitigation |
| --- | --- |
| **Deleting a temp folder follows a link and removes real data** (critical, low likelihood) | Link-safe delete, with dedicated tests on Windows, Linux and macOS before stage D is switched on |
| Tools behave differently with a new temp folder | Per-run sharing, a switch-off, and a full delivery-process regression run |
| Redirected build output surprises a process step | Only for read-only targets; clearly reported; switch-off |
| macOS cannot be tested on this machine | CI host lane required; macOS stays switched off until it passes |
| Test churn | About 650 existing test methods in 28 directly affected classes; only 3 assert today's failure text; stage-by-stage test lanes |

## Retesting range

- **Automated per stage:** the whole Unit suite (fast), focused Integration and Components lanes
  for the touched areas, and new host-platform tests on Windows (local), Linux (Docker, local)
  and macOS (CI).
- **Manual, on 5032 through the UI and API:** force each failure reason, terminal secrets check,
  proxy on and off, agent build on a read-only product, runtime-node and agent-run temp
  lifecycle, crash-and-restart cleanup, the per-agent setting toggle, and **one full delivery
  process run** on a sample app. It touches everything, so it is the most valuable regression
  test.
- **Once at the end:** a CI run with the full platform scope (roughly 1.5–4.5 hours per OS on CI).
  You trigger it and share the log zip, because CI logs are not readable from here.

## Decisions

| When | Decision | Recommendation |
| --- | --- | --- |
| Now (B) | Proxy and certificate variables | Opt-in host setting plus a names-only extension list |
| Now (B) | Terminal mode | Same environment rules as Direct mode (no secrets) |
| Now (C) | dotnet on read-only products | Redirect output to the run's temp folder (switchable) |
| Later (D) | Temp-folder location, granularity, failure handling, per-agent read option | As in 05 §3. Failure handling is revised to *never block startup* |
| Decided | Containers / OS sandbox | Not now |
| Decided | Agent access to temp folders | Per-agent option, not a global rule |

## Out of scope

- OS isolation and containers.
- Localization.
- Changing shared download caches (NuGet, pip, npm, Playwright stay shared).
- Enabling Windows long paths on the machine.
- The Linux secret-tool cancellation detail.
