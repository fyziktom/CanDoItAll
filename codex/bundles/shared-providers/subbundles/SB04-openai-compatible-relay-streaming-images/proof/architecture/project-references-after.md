# SB04 project references after implementation

State: `PASS`; source review, the captured reference transcript, and the force-refreshed
CodeAnalytics snapshot agree.

| From | To | SB04 state |
| --- | --- | --- |
| `CanDoItAll.SharedProviders.Abstractions` | any product project | none; the neutral contract project remains the inward leaf |
| `CanDoItAll.SharedProviders.Http` | `CanDoItAll.SharedProviders.Abstractions` | unchanged SB03 edge; Http has no Workspace, Web, EF, UI, provider-SDK, or Composition reference |
| `CanDoItAll.Composition` | `CanDoItAll.SharedProviders.Http` | unchanged SB03 edge; outer composition owns concrete relay registration |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Abstractions` | unchanged SB02 edge; Workspace owns routing, target, secret, audit, and recovery behavior without referencing Http |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Workspace` | unchanged outer projection edge |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.SharedProviders.Abstractions` | added and authorized; the outer module implements the neutral existing-image capability and usage bridge |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | unchanged; Web maps the three POST surfaces over the Workspace application port and does not reference Http |

The only SB04 product-reference delta is
`CanDoItAll.Modules.AgentFramework -> CanDoItAll.SharedProviders.Abstractions`. It is the exact
inward edge authorized by the before artifact. No inner AgentFramework Models, Providers, Core,
or MAF project gained a Workspace, Http, Web, or EF dependency. The image persistence/current
eligibility lookup remains Workspace-owned; AgentFramework receives a Workspace-resolved target
and invokes the existing image capability.

The Unit project adds a direct test-only reference to Http so the 24 policy Facts test the real
implementation. Test-only references are not product graph edges. The final semantic-repair source
listing is
`bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-project-references-after-semantic-final.txt`.

After snapshot `snap-20260825051057-300644c7` reports 14 scoped product projects, 34 direct
product `ProjectReference` edges, and zero project-level cycles. The two module-level cycles and
one nested-type cycle are the unchanged baseline findings. Any Workspace-to-Http,
Http-to-Workspace/Web/EF, Abstractions-to-product, or inner-MAF-to-outer-project edge reopens this
checkpoint.
