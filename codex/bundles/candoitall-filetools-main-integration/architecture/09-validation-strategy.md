# Validation Strategy

## Proof Tiers

- Standard: SB01, SB05.
- Behavioral: SB02, SB04, SB06, SB09-SB14, SB17.
- Governed: SB03, SB07, SB08, SB15, SB16, SB18.

Governed is reserved for filesystem security, authority/effects, cache/privacy boundaries, resource persistence, editing/migration, and final cross-surface audit.

## Command Families

Execution must resolve exact filters from current test names and record commands/results; do not copy stale counts.

- `dotnet restore/build/test` for affected project/solution with Release and warnings-as-errors where repository supports it.
- `dotnet format --verify-no-changes` for affected solution.
- FileTools pack/validate scripts and SHA-256 artifact intake in SB01/SB06.
- CodeAnalytics scoped snapshot/dashboard/findings/inventory/dependencies before/after reference/architecture work.
- Components MCP discovery before UI and shared watch + persistent Playwright page during UI loops.
- Source audits for forbidden refs, service location, new partials, raw effects, unsigned authority, TODO/FIXME/NotImplemented, duplicate paths, and old-owner shrink.
- Scoped .NET performance recipes from `analysis/03-dotnet-performance-audit.md`, plus generated large-directory/fake-transport structural counters defined in `architecture/10-performance-and-scale.md`.

## Behavioral Semantic Proof

Every Behavioral/Governed subbundle records raw note, shipped behavior, source proof, exact tests, shallow-pass trap, meaningful negative, realistic positive, anti-stub audit, and downstream check in `reviews/01-execution-report.md` or linked proof.

## Governed Artifacts

Use `bundle://proof/SBxx/manifest.md`, semantic invariants, transcripts with command/exit code, source assertions, changed-file hashes, and browser/host/red-team artifacts assigned by the subbundle. Never create manifests for lower tiers merely as ceremony.

## Baseline And Affected Scope

- A failing unrelated baseline is not ignored: capture it, prove it predates changes, and run the narrow affected validation. If it prevents affected proof, the phase is Blocked.
- Test success cannot substitute for CodeAnalytics/dependency review when references change or for real browser proof when UI changes.
- Browser screenshots are inspected, not only captured.
- Performance proof cannot be compile-only, page-count-only, or a single wall-clock assertion. Record runtime/machine facts, repeated timing, inspected/returned entries, metadata calls, retained state, bytes, cancellation, and allocation evidence.
- Direct Project Structure asset proof uses instrumented fakes/spies to assert zero FileBrowser catalog/session/browse/search/cache calls, not merely absence of browser markup.

## Final Closure

SB18 reruns package provenance, affected Release builds/tests/format, dependency/cycle audit, security red-team, representative accepted scale/performance tests, all new non-quarantined Playwright flows, raw-note closure, prepared/completed structural validator, and manual bundle validator. Any contradiction reopens the earliest owning subbundle.
