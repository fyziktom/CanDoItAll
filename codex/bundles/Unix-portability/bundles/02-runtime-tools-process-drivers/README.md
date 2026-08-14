# CanDoItAll Runtime, Tools, and Process Drivers

## Mission

Make runtime-node execution, Manager supervision, local MCP/external tools, plugins/FileTools, and process-domain strategies portable without duplicating process infrastructure or moving process semantics into MAF.

## Status

- `B07 implemented and locally proven — Gate R3 remains GO; hosted Windows/Ubuntu/macOS aggregate and Final Gate R4 deferred`

## Source anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Prepared commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- SDK: `.NET 10.0.302`

## First eligible work

`Hosted execution of the configured B07 Windows/Ubuntu/macOS matrix, followed by independent Final Gate R4 review`

## Entry blocker

Do not execute any implementation task in this bundle against the prepared development anchor. B00 must re-anchor to the exact commits in `../01-core-portability-foundation/reviews/CORE-C4-HANDOFF.md`. The current entry is an operator-authorized provisional implementation handoff; C4, hosted evidence, and macOS support remain deferred and must not be described as passed.

Actual macOS validation is additionally governed by `reviews/07-runtime-macos-validation-deferral.md`. B01–B06 may advance on complete Windows/Linux actual-host proof plus deterministic macOS contract fixtures, but unproved capabilities remain unavailable and final R4 stays deferred until actual macOS evidence exists.

## Split policy

This is already a separate bundle because runtime ownership and integration risk are materially different from core migrations. B00 may split it again into child execution bundles when measured scope crosses the declared triggers.

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

Only `R4` may mark this bundle complete. A green build without actual-host migration/runtime evidence is insufficient.

## Universal rules

Read the program-level [`CODEX-EXECUTION-CONTRACT.md`](../../CODEX-EXECUTION-CONTRACT.md). Preserve unrelated changes, keep source comments in English, do not push without explicit instruction, and stop on every named NO-GO condition.
