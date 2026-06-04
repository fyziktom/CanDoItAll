# Structured Input

## User Constraints

- Do not rush Process Core extraction.
- Continue with smaller isolation steps around the large dispatcher partials.
- Use abstractions/seams first, then migrate concrete sections only when ready.
- Force refactor/checkpoint subbundles every few phases.
- Preserve all original process behavior and prove no tool, artifact, lineage, validation, or projection behavior was dropped.
- Do not run small/medium/mobile UI proof; PC/large-screen only if any UI proof unexpectedly appears.

## Architectural Reading

The previous work successfully isolated AgentFramework execution snapshots and began artifact projection extraction. The next highest-value boundary is not `Processes.Core`; it is local source-adapter and write-coordinator isolation inside `CanDoItAll.Modules.Processes`.
