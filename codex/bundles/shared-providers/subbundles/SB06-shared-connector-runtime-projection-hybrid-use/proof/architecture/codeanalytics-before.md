# SB06 CodeAnalytics before implementation

State: `CAPTURED`.

SB06 starts from force-refreshed snapshot `snap-20260825070408-300644c7`, captured after all SB05
product repairs and before SB06 product edits.

| Fact | Value |
| --- | ---: |
| Scoped product projects | 14 |
| Scoped source documents | 758 |
| Modules | 35 |
| Dependency facts | 5,231 |
| Direct product references | 34 |
| Project cycles | 0 |
| Other governed cycles | 2 module, 1 nested type |
| Error findings | 0 |

The stop condition is any inner MAF reference to Workspace, SharedProviders Http, Web, or UI, any
Workspace-to-Http edge, or any second provider runtime/master. A generic inner transport extension
point is permitted only if it stays connector-neutral and outer Composition supplies the concrete
hardened implementation.
