# CanDoItAll Core Portability Foundation

## Mission

Deliver a stable Windows/Linux/macOS headless core by fixing path and filesystem semantics, migrating storage/control-plane state, securing secrets and key rings, wiring narrow platform composition, and establishing an active three-platform CI gate.

## Status

- `In progress — A07 local readiness GO; C4 pending exact-commit hosted evidence`

## Source anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Prepared commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- SDK: `.NET 10.0.302`

## Active work

`A05 — Platform composition, capabilities, and readiness` is complete with independent Gate C3a GO. `A06 — Headless hosting, publish, installation, and operations` is complete with independent Gate C3b/Hosting GO after remediation of its three bounded review findings. It owns framework-dependent RID artifacts, clean headless publish execution, Unix service/install boundaries, redacted per-purpose diagnostics, durable host provenance, and cross-platform operator guidance. `A07 — Three-platform CI, integration, restart, and Core Gate C4` has a locally validated candidate with current Windows/Ubuntu stable and headless evidence plus local static enforcement. C4 remains pending an exact committed/pushed anchor and hosted Windows/Ubuntu/macOS evidence. Genuine macOS Keychain execution remains separately deferred as `MACOS-KEYCHAIN-VALIDATION-001` and is not required for this bundle progression.

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
