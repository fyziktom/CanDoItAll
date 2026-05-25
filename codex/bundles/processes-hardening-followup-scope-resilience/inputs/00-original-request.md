# Original Request

User request, normalized but preserving intent:

> Codex claims it implemented the previous bundle. I created a separate branch `processes-hardening`.
> Review how it implemented the changes. Analyze the whole process execution mechanism and find weak spots that can lead to unnecessary stopping or blocking.
> We must improve our processes.
> A concrete problem occurred: in a Blazor app process, the first step only asked an agent to create architecture, but the agent also started implementation. Implementation was supposed to be the second step and done by another agent.
> The process core must remain generic for any process type, not only application development.
> Much depends on instructions, step definitions, artifacts, and process templates.
> Analyze thoroughly, propose improvements, prepare a follow-up bundle, and output it as a ZIP.

## Literal Closure Obligations

- Do not narrow the analysis to Blazor or software delivery.
- Treat Blazor as a red-team scenario for generic process governance.
- Do not mix `Processes` and `Workflows`.
- Identify causes of unnecessary blocks, useless retries, missing/invalid artifacts, and step scope drift.
- Produce an implementation-ready bundle for Codex.
