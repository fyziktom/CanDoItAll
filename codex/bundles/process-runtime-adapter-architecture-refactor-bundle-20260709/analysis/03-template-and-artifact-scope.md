# Template And Artifact Scope

## Why This Bundle Includes Template/Artifact Audit

The user called out that the observed blocked process is one example, but similar trouble may exist in other process templates and artifact templates. GPTPro analysis reached the same conclusion: template contracts can make proof gaps, product defects, and tool failures ambiguous. The architecture refactor is incomplete if extracted runtime services only work for the current software-delivery example.

## Scope

Implementation must audit:

- Process templates under `src/Processes/CanDoItAll.Processes.Templates`.
- Any template fragments contributed by process drivers.
- Artifact templates or generated managed artifact conventions used by process runs.
- Launch-variable contributors that generate tool-critical paths, script refs, execution plans, side-effect manifests, required receipts, or acceptance criteria.
- Tests that currently encode unresolved placeholders or prompt-only deterministic plans.

## Audit Questions

For each template or artifact contract:

1. Does it contain deterministic tool-plan requirements only as prose?
2. Does it rely on branch names or domain terms that generic runtime code later hardcodes?
3. Does it declare required receipts without branch applicability?
4. Does it distinguish acceptance proof, defect proof, blocker proof, and missing-proof retry?
5. Does it use unresolved placeholders in tool-critical values?
6. Does it rely on physical file existence instead of accepted artifact ledger slots?
7. Does it have typed acceptance criteria or only narrative text?

## Required Output

SB07 must produce:

- Template inventory table.
- Artifact-template inventory table.
- Migration classification for each item: no change, typed contract required, driver policy required, or obsolete/unsafe.
- Source assertions proving no generic runtime/dispatcher file gained domain terms.
- Regression tests covering at least one non-Tetris/non-calculator process path.

