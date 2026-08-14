# Proof status and failure-classification contract

## Existing evidence

The first-wave closure records:

- Release solution build passed.
- Focused LLM Chat unit/integration/API tests passed.
- Stable filtered solution test ended with 8,121 passed and 19 failed.
- At least seven failures were described as reproducible outside the feature.
- The final decision remained Not Ready.
- No current workflow run exists for the feature-head commit.

Those statements are useful orientation, not final proof.

## SB00 classification procedure

1. Synchronize the feature branch with current development in a clean worktree.
2. Extract the exact 19 fully qualified test names, exception types, and evidence paths from TRX/logs.
3. Run only those exact tests against:
   - synchronized development;
   - synchronized feature branch.
4. Classify each result:
   - `Baseline`: fails identically on development and feature.
   - `BranchInduced`: passes on development and fails on feature.
   - `EnvironmentSensitive`: result depends on a documented prerequisite and is reproducible accordingly.
   - `ObsoleteAfterSync`: no longer fails on either synchronized head.
   - `Unresolved`: evidence is insufficient or inconsistent.
5. CP0 blocks on any `BranchInduced` or `Unresolved` result.
6. Do not run the stable solution gate in SB00. It is reserved for SB13.

## Required artifact

Use `templates/prior-failure-classification.csv` and fill one row per prior failure. Preserve the exact
command, host, database mode, dependency mode, branch SHA, and result.
