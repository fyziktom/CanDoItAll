# Wave stabilization heuristics

Use these buckets:

## Must stabilize now
Use when the new block:
- creates or duplicates canonical truth
- blurs permissions or agent authority
- makes the next feature wave significantly riskier
- lacks minimal invariant tests
- introduces a likely source of hidden data divergence

## Before the next wave
Use when the issue:
- does not break correctness today
- but will amplify quickly if more features stack on top

## Later
Use when the issue:
- is mostly clarity or cleanup
- does not currently threaten correctness or ownership
