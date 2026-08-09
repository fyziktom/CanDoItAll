# A01 portability scan review

## Final scan

- Generator: `scripts/scan_portability.py`.
- Input: `artifacts/unix-portability/A01/post-scan-post-review-final.json`.
- Files scanned: `4,742`.
- Findings reviewed: `25,644`.
- Critical/high findings: `8,319`.
- Unclassified findings after review: `0`.
- Review output: `post-scan-reviewed-post-review-final.json`,
  `post-scan-reviewed-post-review-final.csv`, and
  `post-scan-reviewed-post-review-final.md` under `artifacts/unix-portability/A01`.

The raw lexical scan deliberately reports `FAIL (unreviewed critical findings)` before
classification. The reviewed output is the gate input. The classifier separates
reference-only text, tests/serialized seeds/templates, and executable surfaces, then
assigns executable findings to a named subbundle and requirement. Serialized prompt
seeds and scaffold templates are not treated as executable A01 path owners.

## A01 executable findings

The reviewed scan assigns `488` executable findings to A01:

| Category | Count | Semantic review |
|---|---:|---|
| `absolute-path-field` | 286 | Field/type names and physical owner call sites. Physical roots and candidates are validated before host parsing; aliases remain opaque outside trusted persistence. |
| `path-api` | 149 | `Path` calls in Infrastructure, Git, and MAF physical owners. Owner-root and candidate syntax are both checked before `Path.GetFullPath`; filesystem comparison is host-specific. |
| `path-normalization` | 17 | Every concrete replacement is classified below; none rewrites an untyped physical path. |
| `windows-path` | 36 | Foreign-syntax detectors, field-scoped legacy readers, JSON/string escaping, documentation, URL validation, or logical-key compatibility. No shared Windows development root remains. |

## Slash-replacement audit

All 17 A01 executable `path-normalization` findings have a typed disposition:

| Family | Finding examples | Disposition |
|---|---|---|
| UI route tokens | `MainLayout.Workbench.cs` | Converts an application route/breadcrumb token, not a physical path. |
| Storage logical keys and legacy routes | `FileSystemStorageDriver.cs`, `FtpStorageBrowseDriver.cs`, `FtpStorageDriver.cs`, `IpfsStorageBrowseDriver.cs`, `StorageJson.cs` | Field-scoped logical-key or legacy-route readers. Physical filesystem names use percent-encoded opaque segments; a Unix name containing `\` round-trips as `%5C`. |
| Opaque encodings | `StorageBrowseCursorProtector.cs`, `StorageJson.cs` | Base64url encoding/decoding, not path normalization. |
| External alias codec | `ExternalTargetAliasCodec.cs` | Normalizes only the typed `external-target` protocol. Versioned child segments are reversible percent encoding; the opaque root id does not expose a physical root. |
| Canonical logical paths | `LogicalPath.cs` | Backslash conversion exists only in the explicitly named legacy logical-field reader. The canonical parser rejects `\`, roots, empty segments, `.` and `..`. |
| Managed media matching | `ManagedProjectMediaPath.cs` | Repeated `/` collapse is policy matching only. Backslash is a separator only on Windows; on Unix it remains a filename character. |
| Workspace logical/native boundary | `WorkspacePathPolicy.cs` | Converts canonical logical segments to the current native separator, or normalizes malformed alias text only to produce diagnostics. It never blanket-rewrites a physical candidate. |

The former `FileSystemStorageBrowseEntryMapper` physical-name hazard is closed:
`Append` percent-encodes each physical name, and only the serialized storage key treats
`/` or legacy `\` as key separators. `Unix_backslash_filename_round_trips_through_an_opaque_browse_key`
is included in the A01-owned Windows/Linux suite.

## Windows-root and foreign-syntax audit

- `appsettings.Development.json` and `launchSettings.json` no longer contain a shared
  `%LOCALAPPDATA%\CanDoItAll` root. `ControlPlanePathDefaults` resolves the native local
  application-data directory; the development documentation describes Windows,
  Linux, and macOS behavior.
- `PhysicalPathSyntaxClassifier` is pure and recognizes Windows drive-absolute,
  drive-relative, UNC in both slash spellings, device, Unix-absolute, URI, and relative
  syntax without probing the host.
- Infrastructure physical owners compare classified syntax with the current host.
  Owner roots and candidates are checked before `Path.GetFullPath` in Workspace,
  storage, MAF, Git, project-scoped policy, and external-alias binding boundaries.
- The A01 SharedKernel additions own only pure logical values/codecs and syntax
  classification. Those additions contain no filesystem I/O, process-global binding
  state, data-protection service, or host probe. Existing unrelated SharedKernel file
  utilities are outside this narrower ownership statement.

## Cross-host proof

- Windows final contract including Hosting scope/isolation: `356/356` passed.
- Linux final contract in Docker including Hosting scope/isolation: `356/356` passed.
- Linux A01-owned extended matrix: `537/537` passed.
- Windows final `Path|Workspace|Storage`: `912/912` passed.
- Linux final `Path|Workspace|Storage`: `898/912` passed; all 14 failures are named
  pre-existing/later-subbundle fixtures (source-inspection separators, Manager/watch/
  Tailwind, ProjectStructure/.NET setup, and the obsolete PostgreSQL compose harness).
  No A01-owned test failed.
- macOS actual-host execution was unavailable. The final three-host golden theory
  explicitly covers Windows, Linux, and macOS host syntax policy; POSIX macOS behavior
  is also exercised by the Linux actual-host run. This is an evidence limitation, not
  a claim that Docker emulates macOS.

The contract suites cover separators, case-sensitive Unix containment and alias child
segments, Unicode, colon/backslash Unix names, dot/empty-segment rejection, canonical
and legacy environment tokens, home expansion, routes/URLs, drive/UNC spellings, Unix
roots, legacy migration, restart binding, unbound/rebind behavior, and round-trip
serialization.

## Architecture result

- CodeAnalytics scoped snapshot: `snap-20260809031028-a2e9718e` (`8` changed boundary
  projects, no blocking diagnostics, no project-level cycle).
- Deterministic full graph: `104` projects, `619` project-reference edges, all `104`
  processed topologically, `0` project cycles.
- `CanDoItAll.Infrastructure.Abstractions` has zero direct references.
- Core and Models reference the abstraction port, not the Infrastructure implementation.
- The snapshot's module cycle between existing Infrastructure ControlPlane/Persistence
  namespaces and type cycle inside AgentFramework.Core are not project cycles. The A00
  snapshot already recorded module/type cycles as separate review inputs.

## Gate interpretation

A01-T09's blanket-replacement, shared-Windows-root, owner-divergence, and dependency
direction checks are satisfied by source review plus actual-host tests. The independent
review in `reviews/08-a01-independent-review.md` records Gate C1a GO.
