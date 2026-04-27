# Test Plan: 04 - Finalizer Tools for Critical Decisions


Unit tests:
- Required finalizer missing -> failure.
- Required finalizer called once with valid DTO -> success.
- Required finalizer called multiple times -> failure.
- Finalizer called with malformed DTO -> failure.
- Assistant text cannot override finalizer result.

Integration tests:
- A process decision or selected critical decision is captured through finalizer and validates.
- Invalid finalizer arguments prevent completion.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
