# Watch Benchmark Matrix

## Scope

The benchmark used the exact simple-edit scenario described by the user:

- edit `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- edit `src/CanDoItAll.ComponentKit/Components/PageHeader.razor`
- add a small text suffix and wait until it becomes visible

Tooling used:

- `tools/watch_benchmark.js`
- one persistent browser page per run
- one nearby text edit per run

## Results

| Variant | Target file | Initial page visible | File change logged | Hot reload log | Visible without reload | Result |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| plain watch | `ProjectsPage.razor` | 85.986s | 0.112s | 14.850s | 15.021s | success |
| plain watch | `PageHeader.razor` | 94.003s | 0.117s | 9.068s | 8.891s | success |
| managed env only | `ProjectsPage.razor` | 73.139s | 0.107s | 15.379s | 15.517s | success |
| artifacts path only | `ProjectsPage.razor` | 113.636s | 0.099s | 14.178s | not visible within timeout | failed |
| artifacts path only | `PageHeader.razor` | 146.781s | 0.134s | 16.365s | not visible within timeout | failed |
| managed-like watch | `ProjectsPage.razor` | 116.090s | 0.077s | 14.837s | not visible within timeout | failed |

## What This Means

- Plain watch is already in the same range the user reported. The simple text change becomes visible in roughly 9-15 seconds.
- Managed environment variables alone did not break the loop for this scenario.
- Adding `--artifacts-path` was enough to break the loop, even without MCP.
- The log line `Hot reload succeeded.` is not a reliable proxy for "the browser can now see the new UI."

## Startup Clues From Watch Logs

The initial build also regressed when `--artifacts-path` was added:

| Variant | Initial build time from watch log |
| --- | ---: |
| plain watch | 54.03s |
| artifacts-path watch | 74.13s |

This suggests the isolated artifacts output is hurting both startup and steady-state fidelity.

## Primary Conclusion

The current `SourceWatch` lane should stop using `--artifacts-path` unless a separate proof shows a safe way to preserve browser-visible hot reload with it. Right now the evidence points the other way.
