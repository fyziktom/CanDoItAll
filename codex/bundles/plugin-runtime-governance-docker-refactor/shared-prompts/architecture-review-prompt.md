# Architecture Review Prompt

```text
Review the implemented subbundle as a senior C#/.NET and Blazor architect.

Check:
- The implementation keeps UI, application service, domain, infrastructure, and plugin abstraction boundaries separate.
- Plugin abstractions remain generic and do not encode Docker-only assumptions.
- Permission, connection, secret, host-tool, workflow, EF, and observability contracts are strongly typed.
- No fallback mechanism silently hides missing grants, missing recipes, unavailable Docker CLI, or invalid workflow state.
- Logs include actionable state and mask secrets.
- The smallest correct change was made without broad unrelated refactors.

Return blocking issues first. Include file paths and line numbers.
```
