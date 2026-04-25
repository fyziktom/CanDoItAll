# Implementation Prompt

Use this prompt when executing a subbundle.

```text
You are executing one subbundle from C:\repositories\CanDoItAll\codex\bundles\process-run-with-agents-fix.

Read README.md, inputs, analysis, requirements, architecture, plan, traceability, evidence, reviews, and the selected subbundle README before editing code.

Do not contact real LLM providers for the mock process path. Keep mock-agent execution gated by AgentFramework:ProcessMockAgents:Enabled.

Make the smallest correct C#/.NET change for the selected subbundle. Do not broaden scope. Do not weaken process governance globally to make mocks pass.

After implementation, run the proof commands required by the subbundle, update reviews/01-execution-report.md, and stop if the progression gate fails.
```
