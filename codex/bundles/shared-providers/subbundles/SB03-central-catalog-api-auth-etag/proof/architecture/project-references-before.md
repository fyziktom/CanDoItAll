# SB03 project references before implementation

CodeAnalytics snapshot `snap-20260824235022-a4b340a8` reports 13 scoped projects and 31 direct
production references. The shared-provider/outer-composition subset is:

| From | To | State before SB03 |
| --- | --- | --- |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | present, SB01-owned |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Abstractions` | present, SB02-owned |
| `CanDoItAll.Composition` | `CanDoItAll.Modules.Workspace` | present |
| `CanDoItAll.SharedProviders.Http` | `CanDoItAll.SharedProviders.Abstractions` | project absent |
| `CanDoItAll.Composition` | `CanDoItAll.SharedProviders.Http` | absent |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Http` | absent and forbidden |
| `CanDoItAll.SharedProviders.Abstractions` | any product project | none |

SB03's expected minimal graph is 14 scoped projects and 33 direct references: one project and
exactly the two authorized edges. Test-project references are test-only and excluded.

