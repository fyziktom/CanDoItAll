# Impact analysis and required tests

- Final-diff correlation: `code-analytics_ff4a0f1aaaa94b2e8cca622bf4f118b0`.
- Result: incomplete, Low confidence, `AllSuppliedSuites` fallback.
- Diagnostics driving breadth: `TIA2001`, `TIA3001`, `TIA3002`, `TIA3004`; pre-existing reflection sites also produced `TIA1006` diagnostics.
- Required Unit workspace: 6,236 passed in the full run; the two exact retries passed 2/2.
- Required Components workspace: 1,010 passed, 0 failed, 0 skipped in the authoritative unrestricted run.
- Focused new behavior: Unit 9/9 and Components 3/3.
- Stable and full Playwright were not run because SB06 explicitly forbids them.
