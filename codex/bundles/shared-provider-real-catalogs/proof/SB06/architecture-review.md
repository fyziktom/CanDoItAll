# Administration architecture review

Primary-agent review, not an independent reviewer. Entry design is
architecture/06-administration-boundaries.md. No project, package or sibling repository
changes. Existing large Settings owner loses token form/state; per-provider sharing loses
source-list/dialog state. New components have cohesive, visible responsibilities.

## Boundaries and security

- UI orchestrates existing source management or ApiTokenAdministrationService; it does
  not perform filesystem persistence. Source connection state is mounted only on demand.
- ApiTokenAdministrationService authorizes EVERY issue/search/revoke/delete, not only
  dialog visibility. Default host adapter denies; Web accepts trusted local operator or
  explicit api.tokens.issue. Generic api is not token-administration permission.
- IApiTokenRegistry is the durable storage seam. Its file implementation uses existing
  control-plane paths/private DurableFileWriter and per-record coordination. IDs are typed
  Guid; status is an enum; external JWT identifiers/version are constants. No new DB schema.
- Per-token JSON contains identity, dates/scopes/revocation only. Issuer registers before
  returning a bearer value. Every signed managed token requires an existing active record
  after cryptographic validation. Delete cannot revive a revoked token; corrupt/unavailable
  registry fails closed and logs only token ID and exception type.
- Search scans metadata only when requested, returning bounded pages (25 UI, maximum100
  storage query); auth reads one ID file. This is O(n) administrative search, not a claim
  of indexed million-token search. No bearer, signing key or API credential is persisted
  in the list or evidence.
- Pre-tracking JWTs lack reconstructable issuance history; they retain normal validation
  and original expiry. This explicit compatibility is visible in the dialog. They cannot
  be individually managed in the new registry, and no invented old history was supplied.

## Source and artifact proof

- before-hashes.csv captured six existing token owners before edits. New owners have no
  pre-edit file. changed-files.csv owns final exact hashes; unchanged pricing failure
  owners/fixtures are identified by source-audit.txt.
- source-audit.txt confirms registration, JWT hook, action-level checks, lazy mount,
  removal of old ownership, unchanged project references and no replacement runtime stub.
- No pass-through project/factory layer, runtime partial split, string-dispatched service
  registry, hidden fallback or provider-catalog mutation was introduced.
- Independent file registry tests include restart/reload, parallel operations, paging,
  duplicate/error handling, JSON privacy and all declared scope coverage. Components
  cover cancellation, exact selection, list laziness and denied actions. HTTP tests
  prove real revoked/deleted/invalid-registry denial and legacy crypto/scope compatibility.
- Live Playwright proves the production DI path on Docker, not fake component services.
  200 -> cancel200 -> revoke401 -> delete401 is confirmed with the SAME issued token.

## CodeAnalytics and test selection limits

Before snapshot snap-20260827203310-80990695 and after snapshot
snap-20260827211207-5fd762c7 are recorded in codeanalytics-summary.json. Both are scoped,
not whole-solution proof. The after snapshot further bounds namespaces and has no
blocking errors; 24 informational factory-registration interpretation diagnostics remain.
Six scoped project edges, zero scoped project cycles. Do not interpret differing snapshot
scope/diagnostic counts as architecture defects fixed by this feature.
The snapshot precedes the final one-line short-ID matching condition; that private
predicate adds no dependency or public contract. Final file hashes, registry tests and
the settled MCP ID search own validation of this last change.

Impact analysis could not resolve an issuer change seed through the supplied test
workspaces and found unresolved dynamic dispatch; it returned AllSuppliedSuites. Both
Unit and Integration were therefore run unfiltered in addition to frozen focused scopes.
This is conservative fallback, not a high-confidence exact impacted-test selection.
Known full-suite pricing/seed fixture failures remain explicit; none justifies restoring
fabricated model catalogs or changing unrelated tests in this administration request.

JWT post-validation integration uses the official JwtBearerEvents.OnTokenValidated
contract: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbearerevents.ontokenvalidated?view=aspnetcore-10.0

Gate: requested architecture and security behavior pass. Broader regression findings
are tracked separately; no full-suite-green or whole-solution-clean claim.
