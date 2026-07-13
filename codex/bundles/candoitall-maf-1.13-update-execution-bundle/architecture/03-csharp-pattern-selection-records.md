# C# Pattern Selection Records

## Record 1: Package Compatibility Adapter

## Context

MAF 1.13 may rename types, change method signatures, or adjust run/session/skills/workflow APIs. Compile fixes should localize compatibility changes without changing CanDoItAll product behavior.

## Forces

- extension growth: low for phase 1; future MAF features are out of scope
- multiple implementations: possible only when package APIs vary by surface
- construction complexity: existing runtime construction is already complex
- external SDK isolation: high
- runtime selection: low
- testability: high
- dependency direction: adapter projects may know MAF SDKs; core/process projects must not

## Selected pattern

Adapter, only when direct API drift cannot be handled by a small local call-site update.

## Rejected alternatives

- simpler class: preferred when a small extracted helper is enough
- partial class: rejected as final architecture
- switch statement: rejected for provider/package capability branching unless existing typed enum/config already owns the decision
- service locator: rejected in core behavior
- direct construction: allowed only inside adapter seams and composition root

## New types and projects

| Type | Project | Responsibility |
|---|---|---|
| To be named only if compile errors require it | Existing adapter project | Normalize one changed MAF API surface behind a typed method. |

## Test plan

| Test | Behavior proven |
|---|---|
| New focused adapter unit test if helper introduced | API mapping preserves current CanDoItAll behavior. |
| Existing finalizer/approval/provider tests | Governance still works through the adapter. |

## Proof that this is not fake separation

`SB04` must show that runtime behavior did not move into another large facade, no new runtime partial file is final architecture, and tests instantiate the extracted helper directly if one is added.

## Record 2: Package Decision Gate

## Context

NuGet reports newer preview and dependency-floor packages, but the update goal is conservative MAF 1.13 compatibility, not latest-version adoption.

## Forces

- extension growth: medium for future MAF package adoption
- multiple implementations: package preview availability differs for A2A and Mem0
- construction complexity: low
- external SDK isolation: high
- runtime selection: no
- testability: medium
- dependency direction: package refs must stay in owning adapter projects

## Selected pattern

Decision gate with explicit package matrix and evidence.

## Rejected alternatives

- simpler class: not applicable
- partial class: not applicable
- switch statement: rejected; package decisions belong in evidence, not runtime behavior
- service locator: not applicable
- direct construction: not applicable

## New types and projects

| Type | Project | Responsibility |
|---|---|---|
| None planned | N/A | N/A |

## Test plan

| Test | Behavior proven |
|---|---|
| Restore/build | Package graph is valid. |
| Source scan | Stable 1.8 references are removed only where targeted. |

## Proof that this is not fake separation

The gate blocks unrelated package updates and requires evidence for every preview-package decision.

## Record 3: Architecture Drift Checkpoint

## Context

The package update may expose existing architecture smell in large runtime classes. Fixing all smell is out of scope, but accepting new drift is also not acceptable.

## Forces

- extension growth: high in MAF/tools/providers/workflows
- multiple implementations: high for providers/tools/workflows
- construction complexity: high
- external SDK isolation: high
- runtime selection: high
- testability: high
- dependency direction: critical

## Selected pattern

Checkpoint review, not a production pattern. If implementation adds new construction or selection logic, apply Factory Method or Adapter only with a new pattern record.

## Rejected alternatives

- broad refactor: rejected for this package update
- new partial class as final boundary: rejected
- facade that owns behavior: rejected
- service-location shortcut: rejected

## New types and projects

| Type | Project | Responsibility |
|---|---|---|
| None planned | N/A | N/A |

## Test plan

| Test | Behavior proven |
|---|---|
| Diff review and source scans | No direct process tools, route expansion, or broad warning suppression. |
| CodeAnalytics dependency proof if references change | No new cycles or dependency inversion. |

## Proof that this is not fake separation

The checkpoint cannot pass if the old runtime keeps growing through new partial files, if a new extension still requires editing the old runtime, or if tests still require full runtime construction for newly extracted behavior.
