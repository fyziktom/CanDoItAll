# Independent architecture and QA review prompt

Review the completed Agents seam slice independently from the implementation narrative.

Verify from source and tests that:

- state ownership is singular and typed;
- current URLs and behavior remain unchanged;
- direct EF and external workflow dependencies actually left Razor;
- `AgentCatalogPanel` is controlled and service-free;
- `AgentDetailsDialog` uses only the editor controller plus justified host services;
- the controllers are cohesive workflows, not service bags;
- no wrapper pyramid, new partial, project reference, or AppComponents feature dependency
  was introduced;
- tests use public seams and no private reflection/uninitialized production services;
- focused discovery/results, stable gate, portability, and browser evidence are real and
  current;
- route/sandbox/project-extraction readiness claims match the remaining dependency graph.

Report blocking findings with exact symbols and evidence. Do not approve because tests
pass if ownership remains duplicated or hidden.
