# Validation prompt

For the current task, validate all of the following before declaring success:

1. impacted feature IDs from `03_FEATURE_PRESERVATION_MAP.md`,
2. targeted component tests,
3. targeted Playwright/browser flows,
4. screenshot expectations,
5. performance counters or benchmark evidence,
6. shared-consumer safety (PromptFactory and Sandbox when shared canvas code changed).

If any item fails:
- fix it,
- rerun the relevant validations,
- do not move to the next task until all targeted gates are green.
