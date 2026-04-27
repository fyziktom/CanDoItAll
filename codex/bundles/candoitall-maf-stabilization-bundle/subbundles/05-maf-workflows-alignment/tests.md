# Test Plan: 05 - Incremental MAF Workflow and Orchestration Alignment


Unit/component tests:
- Workflow adapter maps process input to typed workflow input.
- Workflow adapter maps typed result back to process event.
- Checkpoint payload includes process id, step id, run id, session key/state reference, and contract key.

Integration tests:
- Existing calculator or process mock flow still completes.
- A selected sequential subflow runs through MAF workflow harness.
- A checkpointed run can resume from a saved checkpoint in a test fixture.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
