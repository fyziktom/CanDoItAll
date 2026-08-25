# SB02 project references after implementation

CodeAnalytics snapshot `snap-20260824231242-d9fc36b9` reports 12 scoped product projects and 25
direct production references.

| From | To | SB02 result |
| --- | --- | --- |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Abstractions` | added; authorized inward contract edge |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | unchanged; SB01-owned |
| `CanDoItAll.SharedProviders.Abstractions` | any product project | none |
| inner AgentFramework projects | Workspace/Web/SharedProviders.Http | none |
| Foundation/Migrations | Workspace | none; model discovery remains registry based |

The remaining 23 pre-SB01 edges are unchanged and enumerated in the SB01 after artifact. The
24-to-25 delta is exactly one edge, and the project graph remains acyclic. Unit and Integration
project references are test-only and excluded from this product graph.

