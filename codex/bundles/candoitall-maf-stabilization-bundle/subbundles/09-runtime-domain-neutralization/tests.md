# Test Plan: 09 - Runtime Domain Neutralization and Recovery Directive Cleanup


Unit tests:
- Generic repeated tool guard emits neutral message.
- Calculator-specific recovery provider emits calculator guidance only for calculator scenario/process/template.
- Generic runtime source/text does not contain `If this is the calculator process`.

Integration tests:
- Calculator process repair loop still works.
- A non-calculator process never receives calculator-specific recovery hints.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
