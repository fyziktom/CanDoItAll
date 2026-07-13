# Refactoring Checkpoint Prompt

```text
Execute this checkpoint subbundle as a quality gate, not as a feature phase.

Inspect the code produced since the previous checkpoint. Refactor overgrown files, extract helpers, remove duplicated mappers/serializers/handlers, tighten public APIs, add dependency guard tests, and improve diagnostics before any downstream phase begins.

Also re-check the live re-entry constraints from analysis/04-live-repo-reentry-alignment.md: current MAF extension points, existing MemorySourceSnapshot contracts, zero-provider typed behavior, and the native repo's explicit-provider role.

Do not add new product behavior except what is required to make the existing phase correct, testable, maintainable, and observable. If a required foundation is wrong, reopen the owning subbundle instead of patching around it.
```
