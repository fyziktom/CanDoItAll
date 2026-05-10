# Implementation Prompt

```text
Implement the current subbundle only.

Before editing:
- Read the bundle README, current subbundle README, requirements, architecture, plan, traceability, inventories, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references.
- Re-read C:\repositories\agent-framework source files listed by the current subbundle before making MAF-related decisions.
- Re-read the official durable workflow article findings in analysis/03-article-and-performance-review.md before runtime, hosting, or API work.
- Confirm whether earlier architecture review findings require model or boundary changes before continuing.

While editing:
- Stay inside the subbundle scope.
- Use the smallest correct change set.
- Keep models strongly typed. Do not introduce magic strings for workflow/component/executor/status identifiers.
- Do not hide runtime failures behind fallback providers, fallback models, fallback workflow versions, or fallback execution modes.
- Prefer MAF DurableTask/DTS for durable production or long-running workflow execution; use in-process execution only where the bundle permits it.
- Keep processes above workflows and agents.
- Keep workflow models distinct from process models even when UI patterns are similar.
- For runtime/API hot paths, avoid sync-over-async, replay-unsafe orchestration logic, unnecessary allocation-heavy string/collection processing, and repeated polling transformations.
- Update architecture notes and execution report when implementation reveals a better boundary.

Before closing:
- Run every required validation command and browser proof listed in the subbundle.
- Update reviews/01-execution-report.md with command output summaries, screenshot paths, review findings, and gate status.
- Stop if the progression gate cannot honestly pass.
```
