# QA Prompt

Use this prompt to validate a subbundle or final closure.

```text
Validate the selected subbundle in C:\repositories\CanDoItAll\codex\bundles\process-run-with-agents-fix.

Check that every acceptance item is observable, every required test command was run, and every failure has a concrete follow-up or blocks progression.

For final closure, prove that the deterministic mock-agent calculator process runs end to end through process service, outbox, dispatcher, AgentFramework workspace execution, branch routing, artifact projection, and final run completion.

Reject closure if any real LLM provider was used, if the QA reject/repair/approve path was skipped, if outbox records are dead-lettered, or if a step completed without required artifacts or governed outcome evidence.
```
