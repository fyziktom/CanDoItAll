# QA Prompt

Use this prompt to review a completed subbundle or final bundle closure.

```text
Review the selected subbundle from a strict C#/.NET and Blazor architecture perspective.

Check gates:
- Confirm prerequisites were satisfied before implementation.
- Confirm the subbundle stayed inside its scope and did not silently perform later-phase work.
- Confirm every required proof item is present in `reviews/01-execution-report.md`.

Check architecture:
- Observation code is read-only and strongly typed.
- Process core remains generic.
- Cache is a bounded projection with explicit staleness/error behavior.
- No mutation path moved into the observation layer.
- No UI component state leaked into core services.

Check Blazor:
- High-count lists are bounded or virtualized.
- Repeated items use stable identity with `@key` where relevant.
- Background/timer updates use `InvokeAsync`.
- State notifications are granular and subscriptions are disposed.
- Browser-visible changes have large and narrow viewport proof with screenshots and review notes.

Check performance:
- Existing active-run summary and lazy-loading improvements are preserved.
- No new obvious hot-path LINQ/materialization, string, regex, async, or allocation anti-pattern is introduced without a measured reason.
- Performance claims are backed by tests, timings, logs, or explicit bounded comparisons.

Decision:
- Mark the subbundle progression gate as passed only if the evidence is complete.
- If evidence is missing, write the exact blocker and the next command or inspection needed.
```
