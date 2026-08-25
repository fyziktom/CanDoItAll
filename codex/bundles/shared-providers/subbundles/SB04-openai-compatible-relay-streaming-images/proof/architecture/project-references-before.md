# SB04 project references before implementation

State: `CAPTURED`

The SB03 closing graph is the SB04 baseline. The durable source transcript is
`proof/transcripts/sb04-project-references-before.txt`.

| From | To | Baseline state |
| --- | --- | --- |
| `CanDoItAll.SharedProviders.Http` | `CanDoItAll.SharedProviders.Abstractions` | present; Http has no Workspace, Web, UI, EF, or Composition reference |
| `CanDoItAll.Composition` | `CanDoItAll.SharedProviders.Http` | present; outer composition owns concrete registration |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Abstractions` | present; Workspace has no Http reference |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | present; Web uses its existing Workspace boundary |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Workspace` | present outer projection boundary |
| `CanDoItAll.SharedProviders.Abstractions` | any product project | none |

SB04 may add only an inward AgentFramework-to-Abstractions edge if the existing image capability
bridge requires the neutral relay contract. A Workspace-to-Http, Http-to-Workspace/Web/EF, or
inner-MAF-to-Workspace/Http/Web edge is forbidden.
