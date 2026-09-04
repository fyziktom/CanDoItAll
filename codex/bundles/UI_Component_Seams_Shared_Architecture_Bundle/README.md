# CanDoItAll UI Component Seams — Shared Architecture Bundle

**Reference ID:** `CDA-UI-SEAMS-BASE-v1`  
**Bundle kind:** Shared architecture reference; non-executable  
**Status:** Prepared for use by future implementation bundles  
**Prepared:** 2026-09-04  
**Repository:** `fyziktom/CanDoItAll`  
**Observed branch:** `development`  
**Observed commit:** `6c02b644acae3f0d05c648d6b169c82acebefea8`  
**Lifecycle:** Temporary branch artifact; migrate durable decisions to maintained documentation or `CanDoItAll.SharedInfo`, then remove this bundle before merge closure

## Purpose

This bundle defines the common architectural direction for a multi-bundle program that
will reduce coupling in CanDoItAll Razor components, create explicit component boundaries,
prepare route-significant state for later bookmarkability work, and make selected UI
surfaces independently sandboxable.

It is deliberately not a normal implementation bundle. It contains no executable
subbundles, no product test command catalog, no implementation proof folders, and no
instruction to refactor a specific feature. Future implementation bundles must reference
this bundle and own their exact source scope, implementation steps, validation, test
changes, and closure evidence.

## Owner decisions frozen by this reference

1. `CanDoItAll.Components` and `CanDoItAll.FileTools` remain live sibling source
   dependencies during the current UI development workflow. Local NuGet snapshot work is
   not part of this program.
2. The first objective is to untangle component responsibilities and dependency seams,
   not to optimize the development Manager.
3. Components remain in their current projects and folders while their state, intent, and
   I/O boundaries are improved. Physical relocation happens only after the boundary is
   proven.
4. Application-wide UI facilities belong in `CanDoItAll.AppComponents`; feature semantics
   remain owned by their module and may later move into `CanDoItAll.Modules.<Feature>.UI`.
5. Do not create wrappers, facades, interfaces, or view models mechanically. Add a layer
   only when it creates a real ownership, substitution, state, host, or I/O boundary.
6. Bookmarkability routing is a later implementation program, but route-significant state
   ownership must be corrected during component untangling so routing can bind to an
   existing typed model.
7. Existing implementation-shape tests must not freeze accidental architecture. Tests
   that count partial files, assert exact private symbol locations, or preserve source
   layout for its own sake are candidates for removal when the affected area is changed.
8. The next concrete implementation bundle is intentionally not included here. It must be
   prepared only after the current independent test repair on `development` is complete
   and the branch baseline is refreshed.

## Target outcome

The program should converge toward this dependency and ownership shape:

```text
CanDoItAll.Components
    reusable product-neutral UI primitives

CanDoItAll.FileTools
    reusable file interaction and file browser capabilities

CanDoItAll.AppComponents
    CanDoItAll-wide shell, navigation, overlay, record, filter,
    tuning, and host-adapter UI without feature-module dependencies

CanDoItAll.Modules.<Feature>.UI
    feature-owned components, presentation state, intents,
    and narrow UI-facing application ports

CanDoItAll.Web / composition
    route ownership, host wiring, concrete service registration,
    and assembly composition
```

The first refactoring stage keeps the current physical project layout but makes this
logical separation visible in code. Later bundles may perform physical extraction after
the component is sandbox-ready.

## How future bundles must use this base

Every implementation bundle in this program must:

- include the reference block from
  [`templates/child-bundle-reference-block.md`](templates/child-bundle-reference-block.md);
- refresh the repository baseline instead of treating the observed commit above as a pin;
- complete the component boundary assessment template for every primary component or
  coherent component cluster;
- state which rules from this base are applicable and record any approved deviation;
- avoid physical relocation unless relocation is an explicit outcome of that bundle;
- own all focused and broad validation required by its actual source changes;
- remove or rewrite obsolete source-shape tests in its touched area instead of extending
  them;
- report whether the result is route-ready, sandbox-ready, and project-extraction-ready.

## Non-goals

This bundle does not:

- change application source code;
- create the Agents implementation bundle;
- add canonical routes, URL codecs, Push/Replace navigation, or compatibility redirects;
- create a new `*.UI` project or browser sandbox;
- move existing components into `AppComponents`;
- redesign screens or modify visual behavior;
- change live sibling dependency mode;
- modify `dotnet watch`, Tailwind supervision, or the development Manager;
- prescribe one universal component base class, controller, reducer, or event model;
- create permanent tests or documentation that refer to this temporary bundle.

## Program principles

- Separate by responsibility and ownership, not by file count.
- Preserve current behavior while introducing explicit seams.
- Prefer pure functions and records for deterministic rules.
- Prefer existing coherent application contracts over duplicate UI service interfaces.
- Use one feature-scoped controller/facade only when a component currently coordinates
  several services into one UI workflow.
- Keep route-significant state controlled by the route-owning page or workspace.
- Keep transient presentation state local.
- Keep secrets and draft values outside URLs and shared location state.
- Make hidden dependencies visible; remove service locator usage from Razor components.
- A moved method is not an extracted responsibility if the original component still owns
  the same decisions.
- A facade is not a boundary if it merely forwards every old service unchanged.
- A test is valuable when it protects behavior or a durable boundary, not an incidental
  source layout.

## Bundle map

- [`README.cs.md`](README.cs.md) — Czech owner summary
- [`prompt.md`](prompt.md) — instructions for agents consuming this reference
- [`bundle.json`](bundle.json) — machine-readable identity and lifecycle
- [`inputs/`](inputs/) — owner directive and supplied bookmarkability source material
- [`architecture/00-program-context-and-target.md`](architecture/00-program-context-and-target.md)
  — problem statement and target architecture
- [`architecture/01-component-ownership-and-placement.md`](architecture/01-component-ownership-and-placement.md)
  — placement rules for Components, AppComponents, and module UI
- [`architecture/02-state-intent-and-routing-readiness.md`](architecture/02-state-intent-and-routing-readiness.md)
  — state ownership and later routing compatibility
- [`architecture/03-service-io-and-controller-seams.md`](architecture/03-service-io-and-controller-seams.md)
  — dependency extraction decision rules
- [`architecture/04-sandboxability-and-future-project-boundaries.md`](architecture/04-sandboxability-and-future-project-boundaries.md)
  — sandbox and physical extraction readiness
- [`architecture/05-test-and-architecture-guard-hygiene.md`](architecture/05-test-and-architecture-guard-hygiene.md)
  — durable tests versus source-shape tests
- [`architecture/06-anti-patterns-and-decision-rules.md`](architecture/06-anti-patterns-and-decision-rules.md)
  — rejected patterns and compact decision guide
- [`inventories/00-initial-hotspots.md`](inventories/00-initial-hotspots.md)
  — initial evidence-based candidate map
- [`plan/00-program-sequence.md`](plan/00-program-sequence.md)
  — multi-bundle sequence
- [`plan/01-child-bundle-contract.md`](plan/01-child-bundle-contract.md)
  — mandatory contract for future implementation bundles
- [`templates/`](templates/) — reusable assessment and reference blocks
- [`references/00-source-register.md`](references/00-source-register.md)
  — source and baseline register
- [`reviews/00-shared-base-review-checklist.md`](reviews/00-shared-base-review-checklist.md)
  — manual consistency review for this non-executable base

## Change policy

Update this shared base only when a cross-program architectural decision changes. Do not
edit it to record feature-specific implementation detail. Such detail belongs in the
child bundle that owns the feature.

When a durable rule has been proven by several completed bundles, migrate it to maintained
repository documentation or an appropriate `CanDoItAll.SharedInfo` skill. Before final
merge closure, remove this temporary bundle after verifying that no still-active child
bundle depends on it.
