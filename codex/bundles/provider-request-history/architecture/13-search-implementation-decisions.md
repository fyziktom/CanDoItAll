# SB06 implementation decisions

## Read boundary (implemented and verified)

ProviderRequestHistoryService uses neutral IHistoryReadStore and IHistoryCursorProtector
ports. HistoryReadStore implements the scalar PostgreSQL index read and exact bounded owner
metadata. HistoryIndexQuery supplies partition/provider/date/filter predicates and descending
SortAtUtc/Id keyset paging. HistoryEntrySqlProjection explicitly selects metadata columns;
private lease/detail identifiers and all source bodies are absent from list SQL.

HistoryQueryBinding binds normalized UTC filters, provider scope, allowed providers, stable
partition, runtime generation, caller and authorization stamp. Data Protection protects the
versioned cursor. Search is live membership, with no count, OFFSET, multi-page snapshot or
source/provider call. A host-wide four-read limit is stricter than a per-partition four-read
limit and avoids an unbounded dictionary of retired partitions. It rejects overload immediately.
All public history read operations have a ten-second cancellation deadline.

Metadata is explicit and separate from content. Content needs both metadata and content grants,
a selected linked content owner when canonical, owner authority, exact source/version existence
before and after source reading, current index state and read-time expiry. Metadata owner
summaries are bounded to16 plus an explicit more-owners flag. Missing/changed owner content
returns no text. Source readers are keyed by the closed source enum; duplicate keys fail.

Application adds only Microsoft.Extensions.Logging.Abstractions at the repository's existing
MicrosoftExtensionsPackageVersion, matching the Memory/SimpleChats application pattern.
No new project edge, framework/SDK/provider dependency, or diagnostic service interface is added.
Unknown infrastructure failures log only operation ID and exception type and return a safe message.

## Host authority (implemented and verified)

Web uses the existing IInteractiveAccessPrincipalProvider and exact authorization policies.
Add independent api.provider-history.read, api.provider-history.content.read and
api.provider-history.manage scopes. General API/invoke/catalog scopes do not imply these.

The current LocalOperatorAuthenticationStateProvider intentionally leaves GetCurrentAsync
anonymous when HTTP authorization is disabled; preserve that behavior and its existing test.
Add an explicit TryGetTrustedLocalOperatorAsync capability to that existing provider/interface.
It may supply the existing server-created local operator only after an established trusted
interactive circuit, both original/effective address checks, and an anonymous actual user.
It must never override an authenticated bearer identity or authorize raw loopback HTTP.
The default interface/anonymous implementation supplies no such authority.

Only the history Web policy opts into this explicit capability. This supports the normal5032
development host without an authentication-disabled allow-all shortcut. Add the three new
history scopes to the server-created local operator; authenticated users keep their own scopes.

Managed credentials are re-read from the existing token registry on every operation and before
publication. Require current status, matching subject and exact required scope in both validated
principal and registry. Recheck expiry for authenticated nonlocal principals. Hash safe principal/
registry identity, scopes and revisions into an authorization stamp; never hash or persist bearer
tokens/provider secrets. Runtime generation and current storage partition are checked again
after awaits. No query-supplied authority is accepted.

Simple Chat canonical content additionally requires its existing ReadLlmChats policy. Agent and
workflow usage-only readers return Unavailable; nonlocal transcript privilege is not invented.
Missing host access registration explicitly denies through UnavailableProviderHistoryAccess.

## Policy bounds and transaction fence (implemented and verified)

Policy reads, preview and Apply use the same bounded authorized operation discipline, with
Manage independent from content. Update must hold the real runtime write fence for its
transaction and recheck authority after flush before commit, with optimistic policy version.

The existing unbounded retroactive-shortening SQL must be replaced before settings exposure.
Preview reads at most BatchSize+1 matching metadata/detail IDs (maximum1001 each), does not
change policy, and reports a capped result. Explicit Apply reselects under the policy lock.
If either set exceeds the supported bound, reject retroactive shortening atomically and tell
the operator to apply future-request policy only. Never silently shorten a partial subset.
If within bounds, update only those selected IDs in the same policy/audit transaction.
This provides an honest bounded operation without introducing a new background job schema.
Canonical-owner retention remains untouched. No expensive preview/count runs on settings mount.

## Proof obligations

Final gate passed:50unit,69integration and13component cases with matching discovery,
actual PostgreSQL plans, host/local/managed/revocation/partition and real runtime-generation
checks, bounded policy preview and after-flush rollback. The nonzero DateTimeOffset boundary
failed in PostgreSQL before UTC parameter normalization; UTC±4 and UTC now pass. Query plan
artifacts use the final implementation. See ../proof/SB06/validation.md.

HistoryAuthorizedOperation shares deadline/concurrency/authority rechecks across query and
policy services. The prose above describes the implemented contract, not remaining work.
The final source/architecture review passed; scale and actual5032/5210/5212 acceptance remain
SB08. Playwright works; the installed Components MCP is reachable through the existing
isolated ToolHarness despite the built-in transport being closed. No user service was restarted.
