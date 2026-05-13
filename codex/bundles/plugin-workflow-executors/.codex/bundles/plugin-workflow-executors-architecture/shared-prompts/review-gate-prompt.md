# Architecture Review Gate Prompt

```text
Perform the mandatory architecture review for the current gate.

Read:
- README.md
- analysis/*
- architecture/*
- plan/02-review-gates.md
- reviews/01-execution-report.md
- the completed subbundle READMEs
- changed source files

Then:
1. Answer each gate question from plan/02-review-gates.md.
2. Identify duplicated helpers or leaked dependencies.
3. Identify any canonicality drift.
4. Verify proof commands/screenshots.
5. Decide Passed / Passed with documented exceptions / Failed.
6. If Failed, create repair tasks and stop downstream work.
7. Update reviews/01-execution-report.md with the decision.
```
