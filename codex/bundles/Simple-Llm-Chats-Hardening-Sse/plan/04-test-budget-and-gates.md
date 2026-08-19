# Test budget and gates

The machine-readable policy is `test-budget.json`.

## During SB00–SB12

Forbidden:

```text
dotnet test ./CanDoItAll.slnx
dotnet test tests/Unit/... without --filter
dotnet test tests/Integration/... without --filter
Playwright
Category=LiveProcess
Category=LongRunning
Category=Quarantined
```

Each subbundle may use at most:

- 3 affected project builds;
- 4 filtered test commands;
- focused static/source guards;
- one migration/model command only when it owns schema changes.

A repeated command must have a documented reason such as fixing a failed assertion. Do not run the same
large filter after every file edit.

## Checkpoints

- CP0: exact prior 19 tests only.
- CP1: no more than two focused aggregate test commands.
- CP2: no more than three focused aggregate commands, including real HTTP/SSE.
- SB13: one restore, one solution Release build, one stable filtered solution test, one CI matrix run.

## Dependency mode

Choose sibling-source or package mode at entry and keep it consistent within a proof gate. Record
`UseLocalCanDoItAllLibraries` explicitly.
