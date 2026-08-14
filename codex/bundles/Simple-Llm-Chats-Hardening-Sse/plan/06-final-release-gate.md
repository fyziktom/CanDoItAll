# Final release gate

SB13 runs after every targeted proof and guard is green.

## Commands

Use the repository's then-current `docs/testing.md` as authority. At review time the intended sequence is:

```powershell
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test ./CanDoItAll.slnx --configuration Release --no-build `
  --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

Then run the repository CI matrix at the final head. Do not describe a local cross-platform emulation as
a passed hosted matrix.

## Failure handling

- A branch-induced failure blocks FINAL.
- A baseline failure must be reproduced against synchronized development with the exact same command,
  but FINAL remains Not Ready unless repository policy explicitly permits it.
- Environment-sensitive failures need concrete prerequisite evidence, not assumption.
- Do not repair unrelated failures inside this bundle unless they block the feature and scope is formally
  changed.
