# Test Plan: 03 - Contract Validation, Repair, and Typed Execution Runner


Unit tests:
- Top-level primitive/list contract is rejected.
- Valid `ProcessStepOutcomeResult` passes.
- Missing reason fails.
- Completed status with unresolved next-action/user-follow-up fails.
- Failed/Blocked/WaitingApproval statuses require appropriate next actions or escalation details.
- Invalid branch outcome fails when explicit branch selection is required.
- `ProcessStatePatchValidator` rejects protected paths and invalid operations.
- Code/architecture/test plan validators reject inconsistent statuses.
- Repair is bounded and repaired output is revalidated.

Integration tests:
- Structured output present + invalid response -> run does not complete as succeeded.
- Structured output present + valid response -> run completes.
- Process dispatcher reuses the shared validator.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
