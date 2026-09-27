# Process Scratch And Environment Hardening

A simplified bundle: analysis, design, a staged plan and validation. It is not the full bundle
workflow. It exists so that work can resume exactly after a context compaction.

**For a quick overview read [MANAGER_SUMMARY.md](MANAGER_SUMMARY.md).**

## Why

A Project Structure runtime node could not start `dotnet run`. The child got an empty environment,
so temp resolved to `C:\WINDOWS` (MSB1025). A quick repair made runtime nodes inherit the host
temp like every other launcher. The operator then asked for proper hardening: agents and their
processes should use managed temporary space, and the platform should stay predictable and workable
on Windows, Linux and macOS without being so strict that setups fail. Blocked or failed work must
explain *why* and *what to do*.

## Resume procedure (read in this order)

1. [proof/SESSION-HANDOFF.md](proof/SESSION-HANDOFF.md): current stage, last result, next step.
2. [03_PHASES.md](03_PHASES.md): invariants, work items and exit criteria for the current stage. Then [07_TEST_CHECKLIST.md](07_TEST_CHECKLIST.md) and [06_BEHAVIOR_CHANGE_LEDGER.md](06_BEHAVIOR_CHANGE_LEDGER.md) **before** touching any failing test.
3. [02_TARGET_DESIGN.md](02_TARGET_DESIGN.md) and
   [05_IMPACT_AND_ERRORS.md](05_IMPACT_AND_ERRORS.md): the component contracts, the launch
   explanation table and the settings plan.
4. [04_VALIDATION.md](04_VALIDATION.md): the case IDs (V*, J*) the stage must prove.
5. [01_ANALYSIS.md](01_ANALYSIS.md) and [evidence/PROBE-RESULTS.md](evidence/PROBE-RESULTS.md):
   only when a design question needs the facts again.

## Status

| Stage | Title | Status |
| --- | --- | --- |
| P0 | Bundle, analysis, manager summary | Done; A–C approved 2026-09-26 |
| A | Explainable launches (reason codes, remediation on every surface) | **Done** (UI-A1…A3; BC-A13, BC-A14) |
| B | Safe environment (toolchain profiles, never-empty, HOME, NetworkTrust, terminal parity) plus the per-agent script environment gate | **Done** (UI-B1/B2; UI-B3 as a real-shell test; UI-B4 optional, not run; BC-B9…B11 found by the UI and host runs) |
| C | Read-only stays read-only (dotnet `--artifacts-path` to `.build/<key>`, switch-off) | **Done** (UI-C1, UI-C2 full delivery process, API-1) |
| D | Managed temp folders | **Deferred** (not approved yet) |
| V | Cross-platform and end-to-end validation | Windows and Linux done; **macOS M-1 pending (operator runs CI)** |
| Z | Closure per released stage | Awaiting operator review; nothing committed |

## Decisions

| ID | Topic | Recommended | Operator decision |
| --- | --- | --- | --- |
| D1 | Scratch location | New process scratch root with short per-OS defaults | pending (stage D) |
| D2 | Granularity | Per execution run / node session / MCP session / ad-hoc command | pending (stage D) |
| D3 | Failure policy | Never block startup; degraded with host temp, shown on the capabilities page | pending (stage D) |
| D4 | Proxy and CA variables | Opt-in `NetworkTrust` plus a names-only extension list with a denylist | Approved with stage B (2026-09-26) |
| D5 | dotnet on read-only targets | Automatic `--artifacts-path` with explanation and a switch-off | Approved, **modified**: persistent `.build/<key>`, never temp (INV-1) |
| D6 | Terminal presentation | Same environment policy, no secrets | Approved with stage B |
| D7 | OS confinement | — | **Decided 2026-09-26: not now** (larger change, needs Docker) |
| D8 | Agent file tools and scratch | — | **Decided 2026-09-26: per-agent option**, not a global rule |

## Working rules

- No commits unless the operator asks. The working tree also holds the uncommitted runtime-node
  repairs from 2026-09-26; keep them.
- Windows builds: Release, `/m:1`, from PowerShell. While 5032 runs, build test projects with
  `--no-dependencies`.
- Integration tests use the isolated PG18 container, never the 5032 database.
- Every source change goes through the portability static gate
  ([docs/testing.md](../../../docs/testing.md#portability-static-gate)).
- Large-screen UI validation only. User-facing text is English (there is no localization
  infrastructure).

## Layout

```text
MANAGER_SUMMARY.md       one-page decision summary: problems, stages, examples, settings, risks, retest
00_START_HERE.md         this file (status and decisions)
01_ANALYSIS.md           current architecture, launch inventory, findings F1–F14, platform facts, threat model
02_TARGET_DESIGN.md      tiers, components, lifecycle, decisions D1–D8, non-goals
03_PHASES.md             stages A–D, V, Z: work items, tests, exit criteria
04_VALIDATION.md         cases V1–V27, journeys J1–J13, Linux and macOS checklists, regression lanes
06_BEHAVIOR_CHANGE_LEDGER.md every intended behaviour change and which old tests change or must stay
07_TEST_CHECKLIST.md     T-A*/T-B*/T-C* new tests, R-* regression lanes, UI/API procedures, Linux and macOS steps
05_IMPACT_AND_ERRORS.md  error surfaces and the launch-explanation contract, settings cost, decision impact sheets, risk register, retest scope, size
evidence/                probe scripts and measured results
proof/SESSION-HANDOFF.md resumable state
proof/VALIDATION-LOG.md  created in stage A; results with commands and outputs
```
