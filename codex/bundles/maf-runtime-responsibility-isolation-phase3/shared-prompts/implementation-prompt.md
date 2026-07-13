# Implementation Agent Prompt

```text
Implement the current subbundle only.

Before editing, reopen the subbundle README, architecture files, responsibility inventory, and phase plan. Confirm the entry gate is satisfied. Do not implement Financial Strategist, margin calculation, MarkItDown, or document-domain behavior.

Follow the C# architecture rules:
- extract by responsibility, not file size;
- no new final partial-class boundary;
- no nested classes as architecture boundaries;
- no broad Helper, Utils, Common, or Manager type;
- no service-locator shortcut in core behavior;
- tests for extracted behavior must instantiate the extracted owner directly without MafAgentRuntime.

Use the smallest correct change set. Add characterization tests before moving risky behavior. Add isolated unit tests and negative tests for every extracted owner. Run the planned focused build/test commands. Update proof/SBxx/manifest.md, proof/SBxx/semantic-invariants.md, reviews/01-execution-report.md, and reviews/csharp-architecture-gate.md before closing the subbundle.
```
