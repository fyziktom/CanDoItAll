# Required full Unit workspace

- Run label: `SB04-UNIT-ALL-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Unit.slnx --no-restore -nologo -v:minimal`
- Exit code: `1`

```text
Passed: 6,228, Failed: 1, Skipped: 0, Total: 6,229, Duration: 1 m 22 s
Failure: LocalWorkspaceProcessHostTests.ExecuteAsync_reports_timeout_and_kills_the_process_tree
Reason: child-process exit timing assertion returned false.
```

- Run label: `SB04-UNIT-FLAKE-RETRY-001`
- Command: exact failed test selector
- Exit code: `0`

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

- Run label: `SB04-UNIT-ALL-002`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Unit.slnx --no-build --no-restore -nologo -v:minimal`
- Exit code: `0`

```text
Passed: 6,229, Failed: 0, Skipped: 0, Total: 6,229, Duration: 1 m 7 s
```

The complete analyzer-required Unit workspace is clean. Invariant IDs: `SB04-INV-01` through `SB04-INV-05`.
