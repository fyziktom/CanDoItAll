# Implementation Prompt

Use this prompt when executing implementation subbundles.

```text
Implement the assigned subbundle only.

Before editing, reread:
- C:\repositories\CanDoItAll\codex\bundles\maf-1-3-a2a-handoffs\inputs\00-original-request.md
- C:\repositories\CanDoItAll\codex\bundles\maf-1-3-a2a-handoffs\architecture\01-target-solution.md
- C:\repositories\CanDoItAll\codex\bundles\maf-1-3-a2a-handoffs\plan\01-phase-plan.md
- The assigned subbundle README.

Use the smallest correct change. Preserve CanDoItAll layering. Do not leak preview A2A SDK concrete types into Core or persistence unless the architecture review explicitly approves it. Keep process artifact validation strict. When touching agent cooperation, enforce permissions, max depth/correlation, cancellation, and actionable logging.

After implementation, run the subbundle proof commands and update the execution report with exact command results.
```
