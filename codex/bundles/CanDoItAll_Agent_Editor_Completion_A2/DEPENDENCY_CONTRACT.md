# Dependency and ownership contract

The intent is a faster isolated UI loop, not maximum numbers of projects or mandatory API calls.
Record evaluated project/package closures, public signatures, actual renderer descendants,
runtime DI ownership and static assets separately. An Assembly.Load walk alone cannot prove
MSBuild/watch isolation; unresolved references fail rather than count as absent.

## Allowed direction

Production AgentFramework module -> feature renderer(s) and safe presentation contracts.
Scenario host -> same renderer(s), existing neutral components and synthetic query/state fixtures.
Feature renderer -> selected Models/abstractions and neutral UI needed by that renderer.
Existing owner services/registries -> remain in their current product/runtime layers.

A1's Editor.UI closure was recorded as 11 projects and its sandbox as 12. These are historical
observations, not required future counts. All previously protected roots were unchanged in A1.
A2 deliberately expands the full editor; justify/measure its additions. Protect catalog/Overview,
Projects P1/P2, Workspace, Resources and shared component roots from upward/cyclic dependencies.

Do not pull AgentFramework.Core/Voice/Canvas or Modules.* implementation into Editor.UI because
Memory, provider or capability selectors previously came from that broad assembly. Models already
contains selected reusable permission data; judge actual edges, not directory names. Do not move
those persistent wire models into the editor library.

## Reuse tradeoffs

AgentCapabilityList already belongs to `CanDoItAll.AgentFramework.UI`; it is not a new missing
renderer. StorageCatalogSelectionField already has its own Workspace UI leaf. Their real controls
can be composed from production and a full-editor sandbox through narrow typed slots, avoiding
new reverse edges and policy copies. A direct lightweight UI dependency may also be justified by
actual closure/callers; document it instead of hiding it. Do not break old boundary guards casually.

External root SelectedReferenceTable comes from AppComponents. First inspect its actual closure
and existing split libraries. Choose honest composition/reuse or a small justified neutral split,
not a fake table and not a global AppComponents refactor. Preserve all affected callers if a truly
reusable control moves. No framework, interface, DTO, line-count or partial-class quotas.

## Negative controls and composition

Test forbidden transitive backend and unresolved-edge injection. Inspect every public signature,
including generic arguments and delegates. Service lookup inside a delegate/opaque object is still
a backend dependency. Safe view records may carry explicit opaque identity but not a runtime owner.
The production host may retain scoped services and route/confirmation policy; the leaf may not.

The pre-existing migrations aggregate legitimately references application composition. Keep it
classified as an aggregation root, not a new forbidden runtime-to-UI edge. A2 adds no direct
Foundation/MAF owner reference to this feature, no new provider registration and no DB schema.

Assets: scoped CSS, fonts, Tailwind inputs and static web assets are tested in standalone publish
and actual Web. CSS-as-content is not a Web ProjectReference. Do not vendor fonts or binaries in
this handoff. Preserve dependency source/pin selection rules; any sibling edit requires explicit
justification, exact source-pair proof and a delivery plan, not a local-only invisible fix.
