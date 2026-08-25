# SB00 CodeAnalytics after checkpoint

## Capture

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824195319-b6470538` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-24T19:53:19.6756112+00:00` |
| Force refresh | `true` |
| Scoped projects | 11 |
| Scoped source documents | 665 |
| Modules | 31 |
| Dependency edges | 4,566 |
| Direct product `ProjectReference` edges | 23 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 type-level |
| Blocking analyzer errors | No |

The production scope is identical to the before snapshot
`snap-20260824190346-9451b9e9`. SB00 changed only tests and governed evidence, so the identical
project/document/dependency counts are the expected result.

## Comparison

| Invariant | Before | After | Decision |
| --- | ---: | ---: | --- |
| Project count | 11 | 11 | Pass |
| Source documents | 665 | 665 | Pass |
| Direct project references | 23 | 23 | Pass |
| All dependency edges | 4,566 | 4,566 | Pass |
| Project-level cycles | 0 | 0 | Pass |
| Pre-existing module cycles | 2 | 2 | Classified, unchanged |
| Pre-existing nested-type cycle | 1 | 1 | Classified, unchanged |

The two module cycles remain Infrastructure.Persistence ↔ Infrastructure.ControlPlane and
Modules.AgentFramework.Hosting ↔ Modules.AgentFramework. The type cycle remains the outer/nested
`ImageGenerationAgentRuntimeToolProvider` pair. None was touched or widened by SB00.

## Gate decision

Pass. Inner provider/runtime projects gained no outer reference, no project-reference cycle was
introduced, and the preferred two-project shared-provider boundary remains executable. Reopen on
any trigger listed in `project-references-before.md`.

