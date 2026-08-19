# Actual-diff impacted-test analysis

- Run label: `SB04-IMPACT-001`
- Working directory: `repo://`
- Command: `code_analytics_impacted_tests_get` with all 15 changed source/test files, exact new-file line ranges, `behaviorIntent=Unknown`, Unit and Integration workspaces, and a 5,000-member budget.
- Exit code: `0`
- Correlation: `code-analytics_6faa6d0071ef4cb3b73e504c4dfacbf7`

```text
ok: true
isComplete: false
confidence: Low
fallbackKind: AllSuppliedSuites
fallbackReason: Static traversal exhausted its explicit member budget.
workspace health: healthy
source tests: Unit 4,996; all supplied workspaces 5,745
required selection: All supplied suites
conditional selections: 0
diagnostics: TIA2001, TIA3002, TIA3004
```

The public record shape, serialization path, reflection/dynamic dispatch, and traversal budget prevent safe containment. Both complete workspaces were therefore run. Invariant IDs: `SB04-INV-01`, `SB04-INV-05`.
