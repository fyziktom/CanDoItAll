# Test and architecture guard hygiene

## Scope of this document

This shared base owns principles only. Every implementation bundle owns the exact tests
and validation required by its source changes. No product test is permanently tied to
this temporary bundle.

## Test value categories

### Durable behavior test

Protects a user-visible or application-semantic outcome:

- state transition;
- load/save/delete behavior;
- authorization result;
- route-ready intent;
- error handling;
- stale async completion protection.

Keep and improve these tests.

### Durable architecture boundary test

Protects a dependency rule that should survive refactoring:

- `AppComponents` has no feature-module reference;
- module UI has no persistence or web-composition reference;
- Razor components do not use `IServiceProvider`;
- route-owned query keys are not constructed in child components.

Prefer project/assembly graph inspection or a maintained analyzer over fragile source
string matching where practical.

### Temporary migration proof

Shows that ownership actually moved during one extraction:

- old component no longer calls a removed service;
- old partial no longer contains a migrated policy;
- new controller is used by the composition root.

This may be useful during a child bundle but must have an explicit removal/review point.
Do not let migration proof become a permanent snapshot of private implementation.

### Incidental source-shape test

Examples:

- exact number of partial class files;
- exact filename list;
- exact private method name or source location;
- exact order of implementation helpers;
- string search asserting one particular internal syntax when several correct designs
  exist.

These tests should not be added. Existing examples in a touched area should be removed or
replaced with behavior or durable boundary coverage.

## Known current example

`ProjectStructurePageArchitectureTests` currently counts files containing
`partial class ProjectStructurePage` and asserts an exact value of `22`. This protects an
accidental source layout and fails when a valid refactor reduces the count. The relevant
implementation bundle should remove this assertion rather than update the magic number.

The same review must be applied to adjacent source-string assertions: keep only those
that protect a real dependency or migration invariant and rewrite them when a more robust
boundary test is available.

## Partial class policy

- Do not add another partial file as the end-state solution to a large Razor/page class.
- Existing partials may be touched during strangler extraction, but responsibility must
  move into top-level types or coherent child components.
- Generated code, platform-specific partial methods, and framework-required partials are
  valid exceptions.
- No test should freeze the current partial count.
- A child bundle touching a large partial class must state which responsibility leaves the
  partial and whether remaining partials become more cohesive.

## No test inflation through architecture prose

Do not translate every sentence in this shared base into a unit test. Architecture rules
need enforcement only where:

- regression is likely;
- the invariant is durable;
- the test can inspect a stable boundary;
- the maintenance cost is lower than repeated review failure.

Otherwise use child-bundle review and maintained architecture documentation.

## Test cleanup rule for child bundles

A child bundle must inventory tests affected by the old architecture and classify each as:

```text
keep
rewrite
remove
temporary migration proof
```

Do not keep obsolete tests merely to avoid changing the test project. Do not replace a
source-shape test with another magic count or filename snapshot.

## Preferred proof after seam extraction

- direct tests of pure policy/reducer units;
- direct tests of feature controller workflows;
- component tests using explicit state and fake ports;
- project dependency checks after physical extraction;
- browser proof only for behavior that requires a real host.

The test should instantiate the new boundary directly. Full runtime construction is not
acceptable as the only proof that an extracted unit is testable.
