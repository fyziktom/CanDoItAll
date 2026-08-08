# CanDoItAll Runtime, Tools, and Process Drivers

## Mission

Make runtime-node execution, Manager supervision, local MCP/external tools, plugins/FileTools, and process-domain strategies portable without duplicating process infrastructure or moving process semantics into MAF.

## Status

- `Prepared — blocked by Core Gate C4`

## Source anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Prepared commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- SDK: `.NET 10.0.302`

## First eligible work

`B00 after a completed Core C4 handoff`

## Entry blocker

Do not execute any implementation task in this bundle against the prepared development anchor. First complete the sibling core bundle and fill `../01-core-portability-foundation/reviews/CORE-C4-HANDOFF.md`. B00 then re-anchors this bundle to the exact C4 commit.

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
