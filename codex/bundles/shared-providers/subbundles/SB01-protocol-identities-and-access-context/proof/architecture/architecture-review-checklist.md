# SB01 architecture review checklist

Use this checklist after implementation, focused builds, and the three selected test lanes, but
before changing SB01 to `DONE`.

## Baseline and dependency direction

- [x] Force-refresh before snapshot captured: `snap-20260824204913-6a7763ae`.
- [x] Before inventory is healthy: 11 projects, 665 documents, 23 direct product references,
  and zero project-level cycles.
- [x] Two module cycles and one nested-type cycle are classified as unchanged SB00 baseline.
- [x] Force-refresh after snapshot includes the new Abstractions project in the same scope.
- [x] After inventory contains exactly the authorized product edge `Web -> Abstractions` and no
  other new production edge.
- [x] Abstractions has zero package/project references and no forbidden framework/outer namespace.
- [x] Direct `.csproj` and `.slnx` inspection agrees with CodeAnalytics.

## Responsibility and public API

- [x] Each new type has one role: protocol constants, one value object, one codec, one DTO family,
  one port family, scoped state, or middleware binding.
- [x] No broad `Manager`, `Helper`, nested architecture boundary, duplicated ownership, or
  application behavior lives in Abstractions.
- [x] Public API inventory accounts for every public type/member and records why it must be
  public; implementation-only converters/helpers remain internal.
- [x] Exact namespaces and type names are consistent across Abstractions, Web, and tests.
- [x] No new partial class exists and no existing large partial is extended.

## Construction and request pipeline

- [x] Web registers one scoped mutable state and exposes it through the narrow read-only accessor.
- [x] No `IServiceProvider` escapes composition, no `BuildServiceProvider` is called, and no
  static/ambient fallback exists.
- [x] Production and `ApiTestHost` both insert the access-context middleware after the existing
  auth pair and before application endpoint dispatch.
- [x] Middleware validates/binds only; it owns no authorization, persistence, catalog, or relay
  behavior.

## Wire, security, and privacy

- [x] Public JSON is explicit, stable, immutable, bounded, and rejects unknown versions/values.
- [x] Serialization snapshots contain no profile, secret, URI, prompt, tool, attachment, or raw
  error fields.
- [x] Routing IDs are stable, bounded, collision-resistant, publication-scoped, and opaque.
- [x] Access context is optional, exact, bounded, request-scoped, independent from auth, separate
  from W3C baggage, and not forwarded to providers.
- [x] Invalid input uses the native API error envelope without stack traces or sensitive values.
- [x] Source and transcript credential/redaction scans pass.

## Testability and evidence

- [x] `SharedProviderProtocolContractTests` discovers the planned 12 tests and covers meaningful
  positive and negative serialization/version/capability behavior.
- [x] `SharedProviderRoutingModelIdTests` discovers the planned 10 tests and covers stability,
  ambiguity, malformed input, cross-publication behavior, and privacy.
- [x] `SharedProviderAccessContextTests` discovers the planned 10 tests against the real Web host,
  including concurrent scoped isolation and forged-reference authorization failure.
- [x] The changed production/test projects build directly with zero errors and no new warnings.
- [x] Proof manifest, changed-file inventory, semantic invariants, hashes, transcripts, handoff,
  traceability, and root architecture gate all agree with the implementation.

## Closure decision

Block SB01 and keep SB02 locked if any item above is false, if test discovery is zero or differs
without a pre-run amendment, if the after graph introduces an unapproved edge/cycle, or if a
wire/security invariant is supported only by test existence rather than observable assertions.

Independent frozen-code review result: `PASS`. No remaining correctness or security blocker was
found across strict parsing, revisions, capability coherence, defensive copies, routing privacy,
invalid-default guards, base-path semantics, middleware ordering/isolation, or dependency
direction.
