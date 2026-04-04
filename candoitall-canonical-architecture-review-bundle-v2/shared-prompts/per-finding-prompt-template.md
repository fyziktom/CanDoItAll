
# Per-finding prompt template

Use this template with a specific subbundle.

## Prompt skeleton

Implement finding: `<FINDING_ID>`

Requirements:
- follow the subbundle README and implementation plan exactly
- keep changes within the intended phase boundary
- preserve existing behavior outside the finding scope
- add the required negative and positive tests
- run the listed validation commands
- then re-check the result using:
  - `$feature-block-architecture-review`
  - `$architecture-drift-audit`
  - and when relevant `$canonical-model-review`

Output:
- summary of code changes
- test/build results
- remaining risks
- whether the finding can be closed
