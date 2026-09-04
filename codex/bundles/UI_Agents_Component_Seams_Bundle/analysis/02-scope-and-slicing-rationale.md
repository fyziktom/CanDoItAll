# Scope and slicing rationale

## Why Agents is the first vertical slice

- it already has route/query state and therefore exercises the future bookmarkability
  ownership model;
- the shell page contains direct EF access and multi-source orchestration;
- the catalog has clear parent/child ownership duplication;
- the details editor exposes strong testability and sandbox blockers;
- the cluster has substantial existing behavior coverage;
- it is large enough to establish a reusable pattern but can be isolated from provider,
  workflow, voice, and process internals.

## Why all three components belong in one bundle

`AgentsHomePage`, `AgentCatalogPanel`, and `AgentDetailsDialog` currently form one state
and dialog chain. Refactoring only the page leaves child-private detail ownership;
refactoring only the catalog cannot preserve deep-link opening cleanly; refactoring only
the details dialog does not create page-owned stable section state. The bundle therefore
owns the seam chain but executes it through sequential subbundles.

## Why AgentProviderProfilesPanel is excluded

Provider administration is another large workspace with its own list/editor/section
state. Mixing it into this bundle would broaden the controller contract and obscure
whether the first Agents seam works. It remains a named next candidate after this bundle.

## Why no physical move occurs

The purpose is to prove responsibility, dependency direction, and a public test seam
while the current application remains the comparison oracle. Moving assemblies at the
same time would combine logical and physical change, complicate static assets/routing,
and make regressions harder to attribute.
