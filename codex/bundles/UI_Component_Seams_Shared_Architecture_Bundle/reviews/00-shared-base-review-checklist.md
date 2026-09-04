# Shared base manual review checklist

This is a manual consistency review, not a product test gate.

## Identity and lifecycle

- [ ] Reference ID is `CDA-UI-SEAMS-BASE-v1`.
- [ ] The bundle clearly states that it is non-executable.
- [ ] No concrete feature implementation subbundle is included.
- [ ] The observed commit is marked as evidence, not a pin.
- [ ] Temporary branch lifecycle and pre-merge removal are explicit.

## Scope decisions

- [ ] Live Components and FileTools sibling source development is preserved.
- [ ] Package snapshot work is out of scope.
- [ ] Manager and direct watch optimization are later stages.
- [ ] Physical component moves are deferred until seam readiness.
- [ ] Routing implementation is deferred while routing-ready state ownership is required.

## Architecture rules

- [ ] `AppComponents` remains feature-neutral.
- [ ] Module feature semantics remain module-owned.
- [ ] Wrapper/interface inflation is explicitly rejected.
- [ ] Pure extraction, feature controller, and I/O port choices are distinguished.
- [ ] Direct EF and `IServiceProvider` access in Razor are identified as extraction targets.
- [ ] Partial-class growth is rejected as an end state.
- [ ] Sandbox and project extraction have explicit readiness conditions.

## Test hygiene

- [ ] The base does not contain product test commands.
- [ ] Child bundles own validation.
- [ ] Source-shape tests are identified as undesirable.
- [ ] The exact `22` partial count test is documented as a cleanup candidate.
- [ ] The base does not demand a permanent test for every architecture sentence.

## Bookmarkability alignment

- [ ] State taxonomy is preserved.
- [ ] Page/workspace state ownership is explicit.
- [ ] Child components receive state and emit intent.
- [ ] Visual presentation remains independent from route identity.
- [ ] URL binding can be added later without redesigning component ownership.

## Consumer usability

- [ ] Child bundle contract is complete.
- [ ] Assessment template can be applied to one component cluster.
- [ ] Reference block is ready to copy.
- [ ] Initial hotspots are clearly marked as a refreshable inventory.
- [ ] No file claims to implement Agents in this bundle.
