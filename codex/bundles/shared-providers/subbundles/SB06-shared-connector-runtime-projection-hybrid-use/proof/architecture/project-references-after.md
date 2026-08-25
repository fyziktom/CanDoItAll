# SB06 project references after implementation

State: `PASS`.

The source listing is `proof/transcripts/sb06-project-references-after.txt`. The normalized
before/after comparison is `proof/transcripts/sb06-project-reference-delta-audit.txt` and reports
`reference-delta-count: 0`.

SB06 introduced no production `ProjectReference` edge. The established boundary remains:

- inner AgentFramework Models, Providers, and MAF do not reference Workspace,
  SharedProviders.Http, Web, or UI implementations;
- the AgentFramework module consumes Workspace and SharedProviders.Abstractions as the outer
  effective-profile adapter;
- Workspace references SharedProviders.Abstractions and never SharedProviders.Http;
- SharedProviders.Http references only SharedProviders.Abstractions;
- Composition owns concrete HTTP-client selection, access-context propagation, and runtime wiring.

Force-refreshed CodeAnalytics snapshot `snap-20260825100508-300644c7` agrees with the static
listing: 14 scoped product projects, 34 direct product references, and zero project-level cycles.
The two governed module cycles and one nested-type cycle are unchanged.
