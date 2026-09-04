# C# architecture gate

Complete independently at every checkpoint and at final closure.

## State and ownership

- [ ] `AgentsHomePage` is the only route-significant Agents state owner.
- [ ] `AgentCatalogPanel` owns only local presentation state.
- [ ] `AgentDetailsSection` is stable and no semantic code/test relies on raw tab index.
- [ ] No duplicate catalog/editor state machine remains in page, child, and controller.

## Dependency direction

- [ ] no direct EF/persistence access exists in target Razor;
- [ ] catalog component has no feature injections;
- [ ] details dialog has no forbidden direct services;
- [ ] controllers do not depend on Razor instances, navigation, RenderFragments, or dialog
      presentation;
- [ ] no `IServiceProvider` or service bag was introduced;
- [ ] no new project reference/cycle or AppComponents feature dependency exists.

## Pattern quality

- [ ] overview query returns one cohesive aggregate and does not create metric interfaces;
- [ ] catalog controller owns data/mutations, not navigation;
- [ ] editor controller owns external editor workflow and is not pass-through mirroring;
- [ ] no fourth production interface exists without approved addendum;
- [ ] stable existing models are reused rather than copied into duplicate DTO families;
- [ ] no wrapper component pyramid or generic lifecycle base exists.

## Extraction proof

- [ ] moved operations are absent from the old Razor component;
- [ ] direct tests instantiate every new seam;
- [ ] target components render with explicit state/session and minimal fakes;
- [ ] old class responsibilities/dependencies measurably shrink;
- [ ] no additional partial file was added.

## Test hygiene

- [ ] no target private reflection/private invocation/uninitialized concrete service;
- [ ] no file/private member/partial/dependency-count source-shape test;
- [ ] behavior and durable boundary tests remain meaningful;
- [ ] temporary migration checks are kept in proof, not permanent product tests.

## Decision

```text
Checkpoint:
Reviewer:
Source SHA:
Decision: PASS | REOPEN
Blocking findings:
Required owner phase:
Downstream proof invalidated:
```
