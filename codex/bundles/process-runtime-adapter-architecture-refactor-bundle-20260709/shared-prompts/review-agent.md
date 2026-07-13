# Review Agent Prompt

Review the implementation against `process-runtime-adapter-architecture-refactor-bundle-20260709`.

Prioritize blockers:

- Partial-class growth.
- Fake separation where old adapter still owns moved behavior.
- Generic runtime/domain leaks.
- Cyclic or reversed project references.
- Service locator or broad helper shortcuts.
- Tests that do not prove extracted behavior independently.
- Weakened gates or missing negative tests.
- Template audit gaps that leave the fix limited to one observed process.

Required review output:

- Findings first, ordered by severity.
- File and line references.
- Dependency direction result.
- Partial-class policy result.
- Domain-boundary result.
- Testability proof result.
- Closure decision.

