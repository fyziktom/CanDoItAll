# Pattern selection records

These decisions specify responsibilities. Equivalent naming and minimal cohesive helper choices do not need owner approval; update the record and evidence. New project ownership, sibling/API changes, or product navigation changes require concrete scope treatment.

| ID | Decision and reason | Alternatives rejected / constraint |
|---|---|---|
| PSR-01 | Typed workspace state with current route codec adapter; selection, active editor target and URL projection remain distinct | String keys inside child logic; storing all transient state in URL; routing migration now |
| PSR-02 | Cohesive dashboard queries preserve separate Overview, usage-selection and history-host load regions | One eager aggregate for every host; interface per trivial metric; EF moved into another partial |
| PSR-03 | Controlled catalog plus cohesive operations and focused host coordination | Catalog launches global dialogs/chat; page accumulates every operation; blanket prohibition on a justified host adapter |
| PSR-04 | Typed editor section and per-instance session/draft with explicit identity and transitions | Public numeric tab index; DI circuit-scoped mutable session; test-only InitialSession by default |
| PSR-05 | Editor use cases separate pure normalization from external work; introduce real minimal ports where constructor testability requires them | Exactly one giant controller, interface quotas, mirrored service bags, dictionary dispatch |
| PSR-06 | Real components and children under deterministic I/O plus actual production operation/composition tests | Private reflection, uninitialized services, fake-only controller evidence, automatic readiness claims |
| PSR-07 | Reuse lightweight owned contracts; use narrow UI projections for unsuitable implementation-owned read types where justified | Mandatory reuse of heavy DTOs; indiscriminate duplication or incidental cross-module relocation |
| PSR-08 | Preserve current dialog hosting now; specify target/session lifetime and leave future route host solution explicit | Treat section callback as route retention; global CloseAll policy change or new overlay framework incidentally |
| PSR-09 | Prepare smallest useful sandbox candidate and measured follow-up independent of production URLs | Routing as extraction prerequisite; splitting all UI into many projects before measuring benefit |

Each implemented pattern record adds: real callers, dependency list, rejected simpler alternative, tests, lifetime, costs, and extraction implications. A proposed port must describe a user/application operation, not merely rename every method of one existing service. Keep normalization pure where possible and presentation formatting local.
