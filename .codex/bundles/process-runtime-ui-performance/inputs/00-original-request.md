# Original Request

Date: 2026-05-08

Source: user request in Codex thread.

> We still have some performance issues with running multiple processes same time. When I run them the ui page of processes is very slow. True I was running it via visual studio, but it still should run better. Analyze deeply how processes runs and how UI is observing it. there are definetelly some blockers/bottlenecks that must be improved. You must test and measure the results with using time measurement on the core side and when it is optimized than same on side from UI together with playwright mcp and measurement of responses time. You must repair and improve it without breaking general functionality of our processes.

## Raw Notes

- N001: Multiple process runs at the same time make the process UI page very slow.
- N002: Visual Studio debug execution may add overhead, but app-side behavior still needs to be better.
- N003: Deeply analyze how process runs execute and how the UI observes them.
- N004: Find and repair blockers or bottlenecks.
- N005: Measure core-side performance before and after optimization.
- N006: Measure UI response time after core optimization with Playwright MCP.
- N007: Do not break general process functionality.
