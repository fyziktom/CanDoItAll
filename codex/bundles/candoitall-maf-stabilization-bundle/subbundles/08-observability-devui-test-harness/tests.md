# Test Plan: 08 - Observability, Tracing, and Agent Test Harness


Unit tests:
- Observability events include required IDs and omit secrets.
- Validation errors are captured with code/path/message.
- Repair attempts are counted.
- Tool policy decisions are logged/traced.
- Finalizer status is logged/traced.

Integration tests:
- Deterministic process mock/calculator flow emits the expected trace markers.
- Invalid structured output emits validation failure trace.
- Tool policy denial emits policy trace.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
