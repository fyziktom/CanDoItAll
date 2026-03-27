# Proof And Status Updates

## Minimum Update Set

After a completed subbundle, update:

- the subbundle README if it tracks status
- `reviews/01-execution-report.md`
- any proof or artifact references in the bundle

## Proof Format

Prefer concrete proof:

- exact `dotnet test` commands
- exact `dotnet build` commands
- screenshot paths
- DOM or CSS checks
- named files changed when that matters to the bundle

Avoid vague proof:

- “works now”
- “tested manually”
- “UI looks better”

## Follow-Up Rule

If the bundle revealed more work than expected:

- do not bury it in the execution report
- add a concrete follow-up item or subbundle
- describe why it was deferred and what proof is still missing
