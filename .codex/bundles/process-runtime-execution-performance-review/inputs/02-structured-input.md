# Structured Input

## Raw Notes

| Note | Wording | Normalized meaning |
| --- | --- | --- |
| N001 | review of our processes module and how we run them | Inspect process runtime, launch/start, transition, dispatch, persistence, and validation paths. |
| N002 | improve the performance | Apply small, behavior-preserving optimizations where the scan identifies hot-path C# mistakes. |
| N003 | standard troubles/mistakes that people do in C# | Use known .NET performance anti-pattern recipes, not speculative rewrites. |
| N004 | preserve all today's functionality | Existing process behavior, tests, public APIs, dispatch, subprocess, artifact gates, and UI-facing data must keep working. |
| N005 | test some of it with the mockup of agents | Run at least one process mock-agent targeted test if feasible. |
| N006 | testing on few independent cases of building simple .net app | Run a few standalone simple .NET app build smoke cases outside product code. |
| N007 | all our processes logic must remain generic | Do not bake .NET-app-specific rules into process core; specificity belongs to step definitions, agents, tools, and skills. |

## Assumption

The highest-value first pass is runtime execution and dispatch support code, not the already-completed Processes page active-run UI refresh bundle.
