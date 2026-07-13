# Persistent Repair And Four-App E2E Request

## Raw Request Summary

The latest clean Tetris software-delivery process nearly completed, but `quality-repair` retried five times while the UI remained broken. After one retry reproduces the same blocker, further blind retries are unlikely to help. A manager, reviewer, or bughunt specialist should diagnose the evidence and hand a concrete repair target to the implementation agent.

The process must understand that a visible UI error after repair means the repair is incomplete or introduced another defect. It must diagnose and repair the defect, then validate again; it must not merely recapture proof or classify known errors as residual risk. The same policy must be reusable for other deliverables, such as a spreadsheet whose repaired formula leaves a chart in an error state.

Generic process runtime and dispatcher code must remain domain-neutral. Development-specific behavior belongs in templates and isolated drivers and must work for any .NET application, not only Tetris or Calculator.

After repair, rebuild and restart instance 5032 with current process and agent templates, clean prior Tetris projections while preserving the workflow input artifact, clear `C:\programovani\dotnet\output`, and observe a fresh autonomous E2E run. Diagnose and repair any product-caused escalation and rerun from scratch until the process completes well.

Then validate Calculator and create two additional Blazor WebAssembly project structures and autonomous E2E runs:

1. A work-time logger using IndexedDB, note-label suggestions, timezone configuration, historical browsing, and a statistics dashboard.
2. An SVG-heavy interactive application selected to exercise graphical QA, browser evidence, and repair behavior.

All four applications must prove the solution is generic and contains no sample-specific runtime/dispatcher leaks. Agents remain on `gpt-5.4-mini`.

## Incident Evidence

- Root process run: `7d32cae3-1dca-45e7-9014-3e7da9ffa1ae`.
- Blocked step: `quality-repair`, instance `252957e1-b0c2-417e-b222-a4ba5a659215`.
- Five repair executions changed incidental scaffold findings while retaining the fatal Blazor error banner.
- Final two attempts produced the same whole-batch fingerprint, but the persistent fatal banner was present earlier as well.
- Browser console root cause: `Home` requires `TetrisGameState`; the app composition root does not register it.
- Several repair outcomes explicitly acknowledged two current browser console errors and still declared completion.

## Non-Negotiable Interpretation

- Persistent failed proof is an unresolved defect, not residual risk.
- Retry progress is measured against stable diagnostic identities, not only the aggregate diagnostic batch.
- Manager/specialist assistance occurs inside the domain process before human escalation.
- Human escalation remains available after bounded diagnosis-guided repair is exhausted.
