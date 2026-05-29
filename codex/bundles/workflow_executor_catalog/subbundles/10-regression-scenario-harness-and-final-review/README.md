# 10-regression-scenario-harness-and-final-review

## Objective

Close the bundle with durable evidence and scenario coverage.

## Required work

1. Restore/build.
2. Unit tests:
   - validation/catalog,
   - file/folder executor,
   - JSON transform,
   - Markdown render,
   - payload/artifact content,
   - helper node policy.
3. Integration tests:
   - workflow API save/test/run,
   - persistent artifact retrieval,
   - persistent checkpoint/list,
   - plugin observer composition still works.
4. Component tests:
   - Workflows page executor catalog,
   - local folder/file template visibility,
   - artifact links.
5. Scenario harness:
   - create fixture folder,
   - ingest folder,
   - transform document summary list,
   - render Markdown,
   - write report file,
   - verify artifact content.
6. Final architecture review:
   - list implemented executors,
   - list remaining planned executors,
   - list honest durable runtime limitations.

## Acceptance checklist

- No known P0/P1 workflow executor catalog gap remains untracked.
- Final report is honest about what is implemented versus planned.
