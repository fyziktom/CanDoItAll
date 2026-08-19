# Test budget and gates

## Development loop

Each production-changing subbundle receives one primary impacted-test analysis from its actual final diff. Re-query only when:

- the diff materially changes;
- a required test fails and the implementation changes;
- a promotion trigger occurs;
- workspace health or symbol resolution is wrong;
- the subbundle is reopened.

Use focused project builds and required test selectors. Do not repeatedly run all Component, Unit, Integration, Playwright, or Stable tests.

## Browser budget

- SB01: baseline capture only.
- SB05/CP2: one focused workspace/composer/approval browser pass when the composed DOM materially changes.
- SB07/CP3: one focused floating/settings overlay pass.
- SB09/CP5: one final focused regression pass across the named high-risk flows.

Do not run the entire Playwright suite after every subbundle.

## Full/stable gate

A full or unfiltered Stable gate is not the default final step.

It is allowed only when one of these is recorded:

- `code_analytics_impacted_tests_get` returns workspace/all-suite scope;
- a project/public-contract change cannot be contained by owner tests;
- a required owner test fails in a way that indicates broader invalidation;
- DI, generated code, reflection, dynamic dispatch, or serialization makes narrow impact uncertain;
- the architecture review explicitly declares affected-scope proof insufficient.

When triggered, run it at most once in SB09 after focused proof passes. Record why it was triggered and its exact discovery/results.
