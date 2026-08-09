# CanDoItAll Core Portability Foundation

## Mission

Deliver a stable Windows/Linux/macOS headless core by fixing path and filesystem semantics, migrating storage/control-plane state, securing secrets and key rings, wiring narrow platform composition, and establishing an active three-platform CI gate.

## Status

- `In progress — A04 SEC-014 independently GO; Gate C2 remains blocked solely by genuine macOS proof`

## Source anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Prepared commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- SDK: `.NET 10.0.302`

## Active work

`A04 — Secrets, vault drivers, Data Protection, and migration` now has the SEC-014 correction independently verified on Windows/Linux. Windows `Auto` uses current-user DPAPI with `Strong` protection; Unix `Auto` uses `LocalUserFile` with enforced `0700`/`0600` modes, typed `BasicLocal` protection, and a same-user warning. Explicit Keychain, Secret Service, DPAPI, and external-key profiles retain fail-closed behavior. The user-reported Windows command and a Linux container without a session vault both served HTTP 200. Gate C2 and `A05` remain blocked solely by the outstanding genuine macOS SEC-002 proof.

## Runtime boundary

This bundle deliberately excludes Workbench runtime-node execution, Manager process discovery/supervision, MCP/external process integration, Docker/FileTools runtime adaptation, and process-domain capability work. Those surfaces are analyzed in the sibling runtime bundle and remain blocked until C4.

## Structure

- `analysis/` — current findings, risks, source map, delta
- `requirements/` — testable requirements and definition of done
- `architecture/` — target design and ADRs
- `plan/` — sequencing, gates, validation, rollout, commands
- `inventories/` — source, dependency, migration, capability, and test ledgers
- `subbundles/` — execution-ready work packages
- `templates/` — gate/evidence forms
- `reviews/` — preparation and execution reports
- `evidence/` — expected artifact contract

## Completion

Only `C4` may mark this bundle complete. A green build without actual-host migration/runtime evidence is insufficient.

## Universal rules

Read the program-level [`CODEX-EXECUTION-CONTRACT.md`](../../CODEX-EXECUTION-CONTRACT.md). Preserve unrelated changes, keep source comments in English, do not push without explicit instruction, and stop on every named NO-GO condition.
