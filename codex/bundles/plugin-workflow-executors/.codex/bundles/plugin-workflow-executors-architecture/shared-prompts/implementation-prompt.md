# Implementation Prompt

Use this prompt when starting a subbundle.

```text
Implement the current subbundle only.

Before editing:
1. Read this bundle README.
2. Read the current subbundle README.
3. Read reviews/01-execution-report.md.
4. Verify prerequisites and exact source references still exist.
5. Stop and update the execution report if source drift invalidates the subbundle.

During implementation:
- Keep changes minimal and outcome-focused.
- Preserve current workflows and built-in executors.
- Do not duplicate settings/schema/secret/helper code.
- Do not pass arbitrary IServiceProvider to plugins.
- Do not persist raw secrets.
- Do not add remote dynamic plugin code loading.
- Put shared helpers in canonical services/components, not page-local code.
- Use English comments in source code.

After implementation:
1. Run required proof commands.
2. Capture browser screenshots if UI changed.
3. Update reviews/01-execution-report.md.
4. Update the spreadsheet status/checklist if possible.
5. Stop at review gates and answer the gate questions honestly.
```
