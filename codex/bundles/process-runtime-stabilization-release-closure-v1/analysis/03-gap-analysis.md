# Gap analysis toward stable process runtime

## Functionally close
- Backend process launch/execution through process-mock works for representative templates.
- Project/project-structure UI launch works far enough to create a durable run and show steps.
- Scheduler/workflow origin paths exist and stay process-owned.
- Runtime-host read-only diagnostics are bound to real run/step ids in tests.

## Not fully stable yet
- User-facing UI has not proven launch-to-completion for a representative process.
- Runtime-host readback is not visible in run detail UI.
- Latest representative template run has not been live OpenAI smoke-tested.
- Release closure is blocked by ratio/process discipline, even though functional tests are mostly green.
- Manual state tests and automation tests must be separated in naming, filters, and release proof.

## Do not do yet
- No further Process Core extraction.
- No execution-capable drivers.
- No generic runtime driver host that can mutate process state.
