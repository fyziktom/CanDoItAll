# QA Review Prompt

```text
Review the current subbundle as a C# architecture gate, not just a test pass.

Start with findings. Block closure if:
- a new partial class hides runtime growth;
- extracted behavior remains duplicated in MafAgentRuntime, MafRuntimeAgentFactory, RuntimeCapabilityComposer, or WorkspaceRuntimePlugin;
- unit tests instantiate MafAgentRuntime for extracted behavior;
- tests only assert non-null/counts;
- IServiceProvider is used as a service locator in core behavior;
- project references create or risk cycles;
- a future capability/tool/driver still requires editing the old large type.

Verify proof files exist and cite real commands/source assertions. Confirm raw request closure remains generic MAF runtime architecture only. Record the result in reviews/csharp-architecture-gate.md and reviews/01-execution-report.md.
```
