# Subbundle result — M03

## Anchor

- Repository commit before: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00-M02 working-tree changes
- Dependency mode: package
- Windows host: Windows x64; SDK `10.0.303`; runtime `10.0.11`
- Linux host: Docker Linux x64; SDK `10.0.302`; runtime `10.0.10`

## Implemented behavior

`LocalWorkspaceProcessHost` remains the only `Process.Start` owner. Windows launches into a named Job Object configured with kill-on-close and persists its instance identity. Unix launches through a `setsid` stop/attach/resume bootstrap that immediately `exec`s the requested binary, preserving the root PID while creating a dedicated process group before user code runs.

All graceful, force, timeout, cancellation, dispose, detached recovery, Workbench, Manager, and MCP lifecycle paths operate on the same typed boundary. Cleanup ignores caller cancellation after it begins, signals or terminates the whole owned boundary, and verifies that the boundary is empty. Root exit no longer short-circuits descendant cleanup.

The exact root contract now contains PID, start time, canonical executable fingerprint, boundary kind, native identity, and opaque instance identity. Unix fingerprints use `realpath`, eliminating `/bin` versus `/usr/bin` alias mismatches. Startup receipts are schema 3 and reject absent or malformed boundary identities. Diagnostics tolerate process exit, access denial, and `MainModule` races without weakening identity checks.

## Commands and results

| Command | Exit | Evidence |
|---|---:|---|
| Windows unit build | 0 | 0 warnings/errors |
| Windows ProcessHost focused tests | 0 | 11 passed |
| Linux Core rebuild and ProcessHost focused tests | 0 | 0 warnings/errors; 11 passed |
| Windows Manager/Workbench/MCP/dotnet lifecycle group | 0 | 96 passed |
| CodeAnalytics scoped refresh | 0 | `snap-20260812122715-ee223b1b`; no blocking errors; only unrelated pre-existing ReferenceData nested-type cycle |

## Validation reuse/invalidation

- Invalidated keys: process identity contract, dotnet startup receipt schema, ProcessHost output, and M08 integrated Windows/Linux candidate.
- Reused evidence: M01 persisted plan semantics and M02 dependency provenance.
- Reason reuse is valid: M03 changes runtime ownership only and does not alter plan hashing, persistence migration, or dependency selection.

## Residuals

Actual macOS execution remains explicitly deferred to M09. The macOS Unix bootstrap is implemented with the system Perl/POSIX runtime but is not claimed as actual-host verified.

## Decision

`GO`

## Next eligible checkpoint

C1
