# What Is Needed To Work Like Before

The minimum "like before" generic process capability is:

1. UI route loads and shows available process templates.
2. Project/project-structure context can start a process.
3. Process run is persisted with project and launch context.
4. Outbox/dispatch can claim the next executable step.
5. A step can execute via workflow-backed role or direct-agent route.
6. Finalizer closes the step and advances run state.
7. Required artifacts are projected, validated and visible.
8. Run detail UI shows status, steps, artifacts, diagnostics and recovery choices.
9. `.NET` create/modify scenario proves software-development domain helpers still work.
10. Business-analysis scenario proves process core remains generic.
11. Scheduler and workflow-origin start paths create runs through process services.
12. Optional live OpenAI smoke proves real provider plumbing without replacing deterministic tests.
