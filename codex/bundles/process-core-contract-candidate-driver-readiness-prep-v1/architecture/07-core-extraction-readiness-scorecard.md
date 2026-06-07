# Core Extraction Readiness Scorecard

## Scoring Scale
- 5: Ready for a narrow extraction proposal in the next bundle.
- 4: Good candidate after one focused cutline test locks the boundary.
- 3: Possible later, but still coupled to module-local adapters or source payloads.
- 2: Documentation or vocabulary only; no production extraction yet.
- 1: Must remain application-local or infrastructure-local.

## Scorecard

| Area | Readiness | Ownership recommendation | Evidence | Next cutline |
| --- | ---: | --- | --- | --- |
| Route stage order and route eligibility rules | 4 | Candidate for a narrow Core read-model/rule proposal later. | `bundle://proof/SB027/manifest.md`, `bundle://architecture/04-core-readiness-decision-matrix-template.md` | Extract only immutable descriptors and pure decisions; keep handlers, claims, route services, and adapters module-local. |
| Route kind classification | 3 | Candidate later only after adapter payload dependency is removed from the candidate edge. | `bundle://analysis/03-route-source-payload-usage-map.md`, `bundle://proof/SB027/manifest.md` | Prove source-payload-free route DTOs before any Core move. |
| Subprocess lifecycle status mapping | 4 | Candidate for pure rule extraction later. | `bundle://proof/SB018/manifest.md`, `bundle://proof/SB027/manifest.md` | Extract only deterministic status/request builders; keep child-run orchestration, projection persistence, gap journals, and finalizer calls local. |
| Transition request shaping | 3 | Possible later for pure request builders only. | `bundle://proof/SB015/manifest.md`, `bundle://architecture/04-core-readiness-decision-matrix-template.md` | Keep `TransitionStepWithClaimAsync`, leases, service scopes, and reload behavior local. |
| Artifact expectation matching and satisfaction rules | 4 | Candidate for pure matcher/read-model proposal later. | `bundle://proof/SB024/manifest.md`, `bundle://proof/SB027/manifest.md` | Extract only expectation snapshots and pure matching rules; keep storage, workspace IO, projection writes, and recovery lineage local. |
| Candidate hydration and direct-agent assembly | 1 | Must remain application-local. | `bundle://proof/SB012/manifest.md` | No Core extraction until EF readback, technical-agent binding, recovery directive lookup, and project-structure mutation are split from runtime. |
| Claim lifecycle and competing-execution guard | 1 | Must remain application-local. | `bundle://proof/SB003/manifest.md`, `bundle://proof/SB021/manifest.md` | Keep leases, heartbeat, guard selection, and lost-claim handling in the process module. |
| Direct-agent execution, provider repair, retry, and finalizer behavior | 1 | Must remain application-local. | `bundle://proof/SB021/manifest.md` | No Core ownership; this is AgentFramework/application orchestration. |
| Artifact storage, workspace projection, and persistence | 1 | Must remain infrastructure/application-local. | `bundle://proof/SB018/manifest.md`, `bundle://proof/SB024/manifest.md` | Keep filesystem, storage, database writes, and projection persistence out of Core. |
| Future helper-driver vocabulary | 2 | Documentation-only; no production API. | `bundle://proof/SB030/manifest.md`, `bundle://architecture/05-driver-readiness-lane-map.md`, `bundle://architecture/06-driver-safety-permission-model.md` | Do not create driver interfaces or registries until a later bundle proves permission enforcement and runtime ownership. |

## Aggregate Decision
The next bundle may propose a narrow `CanDoItAll.Processes.Core` project only for pure read models and deterministic rules with no EF, workspace, storage, transition execution, claim lifecycle, AgentFramework, finalizer, manager tool, DI registration, or production driver API. The safer first candidate is route/subprocess/artifact pure-rule descriptors; application orchestration remains out of scope.

## Required Next-Bundle Preconditions
- Start with failing architecture tests that prove no application or infrastructure dependency enters Core.
- Move one rule family at a time and keep compatibility adapters module-local.
- Do not introduce production helper-driver interfaces, registries, tools, or runtime dispatch in the same bundle as the first Core proposal.
- Preserve the Gate J no-driver proof and Gate I pure-rule parity proof while adding the first Core cutline.
