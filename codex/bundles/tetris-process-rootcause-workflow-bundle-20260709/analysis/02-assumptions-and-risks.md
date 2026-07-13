# Assumptions And Risks

## Assumptions

- The bundle prepares implementation work only; production code changes start in subbundle execution.
- GPTPro analysis is trusted as source input but must be verified against current repository files during execution.
- Existing process templates are JSON/Markdown resources under `repo://Templates/Processes/processes`.
- The preferred architecture is data-driven runtime routing plus domain-specific template/contributor/provider metadata.
- Existing legacy receipt formats must continue to work for templates not migrated in this bundle.

## Critical Path Risks

- If incident regression tests are weak, later branch routing could silently reintroduce same-step retry exhaustion.
- If completion gate extraction changes behavior before characterization, failures may be attributed to the wrong phase.
- If structured receipt parsing drops legacy string arrays or by-step maps, existing process templates can lose tool visibility.
- If branch route metadata is implemented in generic runtime conditionals, the architecture leak remains, only moved.
- If template migration covers only `software-delivery`, Blazor and .NET slice processes can keep the same branch/repair loopback bug.
- If acceptance criteria matrix generation is vague, Tetris-like products can still pass as shell UIs.

## Validation Risks

- Some end-to-end process runs may require active tool providers, project-structure state, and runtime/browser capabilities unavailable in a unit-only environment.
- The real blocked 5032 instance may have state not reproducible from static evidence; use synthetic regression first, then real process smoke when tools are available.
- CodeAnalytics snapshot loaded successfully but produced non-blocking duplicate-type diagnostics; do not treat those diagnostics as evidence against the target source files.
- Browser proof is required for final process scenarios, but not for this bundle preparation.

## Reopen Triggers

- Reopen SB01 if later phases need direct access to the old adapter or cannot test extracted gate behavior without MAF/runtime construction.
- Reopen SB02 if any legacy receipt format, by-step map, or launch variable is lost after structured rule parsing.
- Reopen SB03 if repair branches still require acceptance-only browser receipts when deterministic defect evidence exists.
- Reopen SB04 if branch routing consumes retry budget or produces no runtime gate findings artifact.
- Reopen SB05 if generic application/runtime code still contains new .NET, Blazor, QA, or software-delivery branch constants after provider migration.
- Reopen SB06 or SB07 if template inventory finds a similar accepted/repair flow not explicitly migrated or exempted.
- Reopen SB08 if acceptance matrix proof only checks existence/counts instead of behavior criteria.
- Reopen SB10 if operator diagnostics cannot distinguish absent, failed, wrong-run, and skipped-by-branch receipts.
