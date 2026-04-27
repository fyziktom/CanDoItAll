# Test Plan: 06 - Session, History, Context, and Compaction Stabilization


Unit tests:
- Session restore path includes current prompt or intentionally omits only when documented.
- Process state summary is present in bounded context snapshot.
- Session history alone cannot determine process completion.
- Compaction is skipped when not eligible.
- Compaction provider selection is configurable or fails clearly.

Integration tests:
- Process step after approval/retry receives current step context without stale hidden-state dependency.
- Existing process mock flow still passes.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
