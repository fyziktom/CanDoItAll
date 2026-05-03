# QA Prompt

Use this prompt when validating a completed subbundle.

```text
Validate the assigned subbundle against its acceptance checklist and proof requirements.

Check the original request and target architecture before reviewing the change. Focus on behavioral regressions, layering leaks, missing permission checks, missing artifact handoff evidence, context loss, and tests that do not prove the claimed behavior.

For review findings, cite exact files and tight line ranges. If UI changed, run browser validation or explain why the change is non-visual.
```
