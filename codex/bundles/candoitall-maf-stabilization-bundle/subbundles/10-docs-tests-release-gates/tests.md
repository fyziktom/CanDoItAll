# Test Plan: 10 - Documentation, Regression Tests, and Release Gates


Documentation checks:
- Docs match implemented code and installed MAF API names.
- New developer can add a typed agent output safely using the docs.

Regression tests/static checks:
- Critical path structured-output preservation.
- Tool policy enforcement.
- Finalizer exact-once behavior.
- No calculator-specific text in generic runtime.
- No markdown-decision parsing in process continuation code.

Release checks:
- Build passes.
- Unit tests pass.
- Focused process/agent integration tests pass.
- Environment-limited tests are documented.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
