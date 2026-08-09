# CanDoItAll Core Portability Foundation

## Mission

Deliver a stable Windows/Linux/macOS headless core by fixing path and filesystem semantics, migrating storage/control-plane state, securing secrets and key rings, wiring narrow platform composition, and establishing an active three-platform CI gate.

## Status

- `Blocked — A04 Gate C2 NO-GO pending genuine macOS Keychain evidence`

## Source anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Prepared commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- SDK: `.NET 10.0.302`

## Active work

`A04 — Secrets, vault drivers, Data Protection, and migration` is implemented and validated on Windows and Linux, including actual DPAPI and Linux Secret Service execution. Independent Gate C2 review and bounded re-review closed the rollback and scanner findings. C2 remains NO-GO solely because genuine macOS Keychain execution is unavailable and required by SEC-002/A04-T11. `A05` remains blocked.

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
