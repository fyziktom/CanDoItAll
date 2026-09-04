# Owner directive normalized for this shared base

## Program direction

CanDoItAll has grown large enough that UI iteration through the full application and
`dotnet watch` is expensive. The immediate architectural program will not change the
live sibling source setup for `CanDoItAll.Components` and `CanDoItAll.FileTools`. Instead,
it will reduce coupling inside application and feature Razor components so selected UI
can later move into lighter projects and browser sandboxes.

## Required sequencing

1. Keep existing components in place.
2. Untangle their ownership, state, intent, and I/O boundaries.
3. Preserve working behavior and use the current application as the integration anchor.
4. Prepare components for later bookmarkability and route-driven overlays.
5. Move only proven components into `AppComponents` or module-specific UI projects.
6. Build module/UI sandboxes after a real lightweight project boundary exists.
7. Optimize direct `dotnet watch` and the development Manager as later, separate work.

## Placement intent

- `CanDoItAll.Components` and FileTools remain the home of general reusable libraries.
- `CanDoItAll.AppComponents` is for application-wide UI that is not owned by one feature
  module.
- Feature-specific components remain with their owning module and may later move into a
  module-specific UI project.
- Reuse by multiple consumers does not automatically make a feature component
  application-generic.

## Abstraction restraint

Do not solve coupling by wrapping every component or service. The program should use:

- pure extracted policies where possible;
- one coherent feature controller/facade where orchestration truly spans several services;
- explicit interfaces only for genuine I/O or host substitution;
- typed state and intent where state ownership is currently fragmented.

## Test direction

Implementation-shape tests that preserve accidental structure are not a desired long-term
guard. In particular, tests that assert the exact number of partial class files are not
meaningful architectural protection and can obstruct valid simplification. Child bundles
must clean such tests in the touched area and protect behavior or durable dependency
direction instead.

## Bundle lifecycle

This shared base exists only during the multi-bundle branch program. It must not become a
permanent product artifact. Before merge closure, durable rules are migrated to maintained
documentation or `CanDoItAll.SharedInfo`; this bundle and completed temporary child
bundles are then removed according to the normal branch closure process.

## Coordination hold

No concrete Agents refactor bundle is included here. It should be prepared after the
currently running independent test repair on `development` is complete.
