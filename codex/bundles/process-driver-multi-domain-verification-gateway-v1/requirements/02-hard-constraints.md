# Hard Constraints

- Preserve existing runtime behavior.
- Keep Process Core deterministic and dependency-clean.
- Keep driver packages read-only unless a future explicit approval changes that.
- No broad Process Core runtime extraction.
- No generic driver registry, selector, host, provider runtime, DI registration, manager command, scheduler hook, workflow hook, or execution-capable driver.
- No shell execution, package restore, external connector calls, Office/Graph calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, provider repair, or retry scheduling.
- No UI, browser, small-screen, medium-screen, mobile, screenshot, or media proof unless UI/media files unexpectedly change; in that case fail and re-scope.
- Every critical gate must include semantic adequacy proof, not just build success.
