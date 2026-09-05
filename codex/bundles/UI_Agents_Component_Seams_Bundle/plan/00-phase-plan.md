# Sequential phase plan

All phases are **Not started**. The current request authorizes bundle revision only.

~~~mermaid
flowchart TD
    SB01["SB01 Baseline, behavior and iteration measurements"] --> SB02["SB02 Workspace state and lazy read operations"]
    SB02 --> SB03["SB03 Controlled catalog and host effects"]
    SB03 --> SB04["SB04 Editor section, session and host lifetime"]
    SB04 --> SB05["SB05 Editor operations and child boundaries"]
    SB05 --> SB06["SB06 Coverage and test coupling audit"]
    SB06 --> SB07["SB07 Production integration and closure"]
    SB03 -. "candidate assessment" .-> Handoff["Separate small extraction / sandbox follow-up"]
    SB07 -. "validated handoff" .-> Handoff
    Navigation["Separate bookmarkability design and implementation"]
~~~

No routing prerequisite points to extraction/sandbox. Ordinarily follow-up implementation starts after this child's closure; an earlier catalog branch/handoff requires explicit scope and source reconciliation, not concurrent edits to the same slice.

| Phase | Primary ownership outcome | Proof tier | Gate |
|---|---|---|---|
| SB01 | Current behavior/test/graph baseline and performance measurement protocol; characterization tests may be added in future execution | Standard | Exact baseline and named scenario map; ambiguities classified before affected work |
| SB02 | Typed workspace state, current route mapping, lazy overview/usage reads and chat context | Behavioral | Existing routes/history-host behavior and actual query composition pass |
| SB03 | Controlled catalog, cohesive operations, host intent/results and extraction candidate | Behavioral | Selection/open/repair/team/chat/context preserved through real host |
| SB04 | Typed details section and per-instance session/target/edit-context lifetime | Behavioral | Real sections/children render; reset/target/stale behavior explicit and proven |
| SB05 | Real editor operations/adapters/policies and necessary same-module child seams | Behavioral | Save/version/capability/delete/partial failure semantics plus production wiring pass |
| SB06 | Complete behavior-to-test map and no private test coupling in affected harnesses | Behavioral | No deferred scenario gaps; exact adjacent navigation case migrated |
| SB07 | Stable production integration/browser/architecture closure and handoff | Governed | Required focused/stable/portability/browser proof and honest readiness verdict |

Each phase carries source scope, named expected cases, observed-versus-new behavior, UI composition decision, dependency impact, artifacts, rollback and invalidation. Tests migrate in the phase that changes their seam. SB06 cannot be used to justify broken earlier gates.

No phase is parallel-safe on the same source tree. A later phase reopens earlier proof whenever it changes the earlier contract, state lifetime, load timing, UI composition or operation result semantics.
