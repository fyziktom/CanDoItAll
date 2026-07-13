# Definition of done and risks

## Definition of done

Codex work is done when all of these hold:

1. Calculator still passes end-to-end.
2. Tetris-like run no longer escalates at QA when a repairable product defect is found.
3. `quality-accepted` requires full applicable acceptance proof.
4. `repair-required` does not require acceptance-only browser proof when deterministic defect evidence exists.
5. `quality-accepted + deterministic scaffold/content failure` routes to the configured repair branch.
6. Retry budget is not consumed by branch-routable deterministic defects.
7. Generic runtime/application code has no new .NET/Blazor/Tetris/software-delivery hardcodes.
8. Existing tests are updated and new incident regression tests fail before the fix and pass after it.
9. Diagnostics expose applicable/skipped gates and routing decisions.
10. Project-structure acceptance criteria are represented as explicit artifacts and used by implementation and QA.

## Main risks

### Risk: overfitting to Tetris

Mitigation: all new generic behavior must be data-driven by template metadata and branch outcome keys supplied as data. Tetris words are allowed only in test fixtures/project-structure samples.

### Risk: accepting repair branches too easily

Mitigation: repair branch should require concrete defect evidence. Missing acceptance proof caused by QA omission is not defect evidence.

### Risk: hiding wrong QA artifact behind runtime branch route

Mitigation: when runtime routes acceptance failure to repair, it must write/append runtime gate findings so downstream repair sees the real defect.

### Risk: breaking legacy process templates

Mitigation: structured receipt rule parser must support legacy string arrays and newline strings.

### Risk: moving too much into generic contracts

Mitigation: generic contracts may know branch/purpose metadata, but not domain semantics. DotNet/software-delivery contributor decides which rules to emit.
