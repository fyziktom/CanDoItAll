# QA Prompt

Validate the completed executor bundle as a skeptical reviewer.

Check:

- Executor contracts are plugin-ready but not overbuilt into a fake plugin runtime.
- `MafWorkflowCompiler` invokes executor nodes and no longer treats them as pass-through.
- ClosedXML is isolated to `CanDoItAll.Tools.Documents`.
- Timeout/retry policy is shared and validated.
- UI exposes executor creation through grouped right-click actions and a workflow toolbox.
- The scenario matrix covers at least 20 real examples, including non-happy paths.
- `gpt-5-mini` and Ollama `gptoss20b64k` attempts are documented with proof or exact blockers.

Return findings first, ordered by severity, with file and line references where possible.
