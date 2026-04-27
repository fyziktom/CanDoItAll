# Test Plan: 02 - Preserve Structured Output Across Approval and Background Continuations


Unit tests:
- A pending approval checkpoint stores enough metadata to recover the structured-output contract.
- Manual approval continuation calls runtime with the contract restored.
- Auto-approved continuation path preserves the same contract.
- A governed process-step continuation without a resolvable contract fails clearly.

Integration tests:
- Simulate a process step that triggers a tool approval and then returns final `ProcessStepOutcomeResult` after approval.
- Invalid JSON after approval does not complete the step.
- Valid structured outcome after approval completes as expected.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
