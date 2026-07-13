# Original Request

Captured on 2026-06-29 from the user request.

```text
Use candoitall-bundle-workflow to solve this:
You are preparing bundle now. Do not do any implementation.

Main goal:
isolation of workflows and their nodes to own projects for better maintainibility and testability.

Architect notes:
- we did similar isolation around tools and skills. It helps to make architecture more clear and do not mix everything in MAF wrapper that started to be too large.
- we have now workflows directly in MAF wrapper. You must identify all parts and prepare how to do workflows and their executors abstractions and helpers (executors must have own) and then projects with implementations. 
- workflows must have proper builders/factories, etc. Executors are now mixed all together. They must be split by logical categories. Lots of executors will come from the plugins, but still we will have lots of default ones too for different groups of tasks.
- this touch a lot to plugins. they are source of another executors. you must analyze it deeply and identify all consequences. 
- use analyzing-dotnet-performance and optimizing-dotnet-performance and other dotnet related skills to analyze proposed architecture improvements and their consequences.
- use xlsx for proper mapping of all parts that we must rework and improve and test. 
- You must analyze plan if it makes logical steps where new projects with abstractions and other parts will be build first (from base up) and tested and then adoption of those new drivers will be done steb by step based on dependency level.
- add checkpoints forced refactoring-hardening subbundles to assure that each phase that closes logical block will go via standard hardening and assuring that we build on good base.
- it is long run and bundle can be larger with more steps. It is ok, just assure they are detailed and codex will not loose the track.
```

## Explicit Constraints Preserved

- Preparation only.
- No production implementation during bundle preparation.
- Base-up project extraction.
- Step-by-step adoption by dependency level.
- Workflow executors must have their own abstractions/helpers and implementation projects.
- Plugin executor consequences must be analyzed deeply.
- XLSX mapping is required.
- Refactoring-hardening checkpoints are mandatory.
