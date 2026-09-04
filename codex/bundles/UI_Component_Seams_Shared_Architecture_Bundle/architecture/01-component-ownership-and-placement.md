# Component ownership and placement

## Placement model

| Location | Owns | Must not own |
|---|---|---|
| `CanDoItAll.Components` | Product-neutral primitives and reusable browser UI behavior | CanDoItAll feature concepts or application services |
| `CanDoItAll.FileTools` | Product-neutral file browser and file interaction capabilities | CanDoItAll module semantics |
| `CanDoItAll.AppComponents` | CanDoItAll-wide shell, navigation, overlay, record, filter, tuning, and host-adapter UI | References to concrete feature modules or feature business rules |
| Current feature module | Feature page/container/component behavior during in-place seam extraction | New hidden cross-module coupling |
| Future `CanDoItAll.Modules.<Feature>.UI` | Feature-owned components, presentation state, intents, and narrow UI-facing contracts | Persistence implementations, web composition, unrelated feature implementations |
| `CanDoItAll.Web` / composition | Route and host ownership, concrete registrations, assembly composition | Reusable feature rendering logic |

## Decision rules

### Put a component in `CanDoItAll.Components` when

- it can be named without a CanDoItAll business noun;
- it is useful to external consumers;
- it requires only component-local browser or rendering capabilities;
- its API can be explained without Projects, Agents, Processes, Workspace, CRM/HR, or
  other feature concepts.

### Put a component in `CanDoItAll.AppComponents` when

- it is specific to the CanDoItAll application experience;
- it is not owned by one feature;
- it provides shell, navigation, overlay, record-browser, filter, tuning, or host-adapter
  behavior;
- adding it does not require an `AppComponents -> Modules.*` project reference.

### Keep or later place a component in module UI when

- its name or state contains a feature noun;
- its behavior implements feature policy;
- its contracts use feature-owned IDs, editor models, commands, or results;
- reuse by other modules still means consuming the owning feature's UI surface;
- moving it to `AppComponents` would require feature references or leak feature types into
  the application-wide layer.

## Reuse does not redefine ownership

A component used by two modules remains feature-owned when one module defines its
semantics. Consumers should reference the owning module UI contract rather than moving the
component downward merely to avoid a reference.

Example:

```text
ProviderRequestHistoryPanel
    remains AgentFramework-owned even if Workspace links to it

PagedRecordBrowser
    can remain AppComponents-owned because its behavior is feature-neutral

AgentDetailsEditor
    remains AgentFramework-owned even if opened from CRM/HR
```

## Current-phase rule: no physical relocation

During logical seam extraction:

- preserve namespace and physical location unless a child bundle explicitly says otherwise;
- allow new top-level records, pure policies, controllers, or ports near the current
  component;
- avoid creating a new project merely to host an unproven boundary;
- record the likely future destination in the component assessment;
- prevent new dependencies that would make the future move harder.

## Suggested future internal organization

`AppComponents` may gradually organize by responsibility:

```text
Navigation/
Shell/
Overlays/
Records/
Filters/
FileTools/
Tuning/
```

A future module UI project may use:

```text
Components/
Pages/
Presentation/
State/
Intents/
Scenarios/
```

Do not create folders solely for symmetry. Add them when multiple cohesive files need the
boundary.

## Hard dependency rule

`CanDoItAll.AppComponents` must remain independent of concrete feature modules. A proposal
that requires adding a reference from `AppComponents` to `CanDoItAll.Modules.*` is
presumed wrong and requires architecture review.
