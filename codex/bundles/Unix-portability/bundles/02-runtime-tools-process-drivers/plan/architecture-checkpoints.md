# Runtime architecture checkpoints

## Checkpoint R0-D — Discovery and responsibility slicing

Status: `Complete`

- Exact main and sibling source anchors are recorded.
- All prepared source references resolve.
- Direct process, shell, recovery, executable, environment, watcher, MCP stdio, Docker, FileTools, native security, process-driver, and test-host surfaces are classified.
- The ownership map keeps process-domain semantics in Processes and native-secret semantics in Security.
- CodeAnalytics snapshot `snap-20260810211432-d225a84b` has no Error findings and no project-level dependency cycle; existing module/type cycles are later bounded inputs.
- The existing B01–B07 split satisfies the size and ownership triggers.

Decision: `GO to R0 architecture review`.

## Checkpoint R0-A — Target boundaries and pattern selection

Status: `Complete — Gate R0 GO`

- B01 owns host-correct execution primitives, lifecycle, environment, and executable identity.
- B02–B06 retain owner-specific compilers and adapt execution rather than moving semantics inward.
- Strategy and Adapter are the default patterns; Factory is permitted only for a real scoped lifetime.
- No new project boundary is approved at B00. A dedicated inward abstraction may be proposed only if B01 dependency analysis proves current references cannot preserve direction.
- No new partial-class split is approved. Changed large types must extract an independently testable responsibility instead of redistributing methods.

Decision: `Independent Gate R0 GO; B01 may enter implementation`.

## Checkpoint R1-I — B01 implementation entry

Status: `Ready`

Required before edits:

- independent acceptance of the B00 inventories and boundary map;
- one failing-first or named characterization per changed behavior;
- affected project dependency graph check;
- named Windows regression first and Linux actual-host proof after the affected slice is green;
- explicit handling of any new source or external dependency discovered during implementation.

## Checkpoint R4-C — Final runtime closure

Status: `Blocked`

Final closure requires all runtime subbundle gates, refreshed full-suite evidence on the exact final candidate, hosted proof when the branch policy permits it, genuine macOS evidence under the recorded operator deferral, redaction and checksum closure, and final independent review. No earlier checkpoint may claim that support level.

## Checkpoint R2-B04 — MCP and external-tool implementation entry

Status: `Approved for B04 implementation`

- `LocalWorkspaceProcessHost` remains the sole production owner of `System.Diagnostics.Process`; B04 adds only a derived duplex-stdio session contract needed by the MCP protocol.
- The MCP project retains descriptor, framing, JSON-RPC, and setup diagnostics while adapting executable resolution, environment shaping, lifecycle, cancellation, and cleanup to B01 contracts.
- Resolved executable identity is authorized with host-correct name semantics after canonical resolution. External JSON tools pass their capability-owned allowlist to the composition adapter for the same post-resolution check.
- Playwright MCP uses an exact package version, a versioned workspace-managed tool directory, atomic winner-preserving publication, and a package-version/CLI-hash marker. Global npm cache discovery is not an authority.
- No project extraction or new dependency edge is approved. The existing Core <- MCP and Core <- Maf dependencies support the adapter shape.
- The requested CodeAnalytics scoped snapshot was rejected by the service because it could export private source. B04 therefore uses local project-reference, source-owner, direct-`Process`, and dependency-cycle assertions without bypassing that security boundary.
- Validation uses named MCP/process/external-tool regressions first, affected-project builds second, and Windows plus Docker Linux B04 slices at the gate. The broad solution suite remains reserved for aggregate closure.

Decision: `GO for B04 implementation; Gate R3a remains blocked pending proof and independent review`.
