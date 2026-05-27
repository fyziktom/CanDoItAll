# MAF 1.6 Feature Checklist For This Bundle

Use official release notes and local package symbols to classify:

| Feature | Expected action |
| --- | --- |
| IChatMessageInjector | Adopt if symbol exists and works with current providers; otherwise prove unavailable and continue with context provider/finalizer fallback. |
| MessageAIContextProvider | Already used; add tests proving context contribution survives function/tool loop. |
| AgentSessionFiles / hosted files | Adopt if symbol exists; otherwise prove unavailable and keep CanDoItAll storage as authoritative. |
| Stream-error input persistence | Add regression around failed stream/tool loop preserving input/session state. |
| Tool approval and MCP metadata forwarding | Prove all tool classes route through policy and metadata is available where possible. |
| A2A v1 | Prove local handoff and remote/hosted A2A surface or explicitly guard unsupported paths. |
| Workflow expected output / ground truth | Adopt for deterministic process/workflow tests if package API supports it. |
| OpenTelemetry auto-wiring | Prove no double wrapping and trace correlation from agent run to process journal/tool receipts. |
| SkillFrontmatter / skills discovery changes | If using MAF skills API, adapt; if CanDoItAll uses its own skills only, record boundary and test no regression. |
