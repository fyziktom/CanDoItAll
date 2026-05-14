# QA Prompt

```text
Review the completed subbundle.

Focus on:
- Whether every covered input is closed.
- Whether the implementation stayed inside the subbundle scope.
- Whether workflow models are strongly typed and not raw MAF/persistence leaks.
- Whether process orchestration remains above workflow/agent execution.
- Whether runtime failures are explicit and observable.
- Whether DurableTask/DTS guidance from the official article is followed for durable workflow execution.
- Whether in-process execution is restricted to approved non-durable scenarios.
- Whether workflow runtime/API hot paths have a performance scan or review note covering async, serialization, polling, and event-stream processing.
- Whether validation proof matches the requested depth.
- Whether browser-visible changes have maximized large-screen and narrower-width evidence.
- Whether architecture review findings were either fixed or explicitly accepted with rationale.
- Whether downstream dependencies can safely proceed.

Return findings first, ordered by severity, with file paths and exact evidence gaps.
```
