# Search, Authorization And Detail Contract

## Search Contract

`ProviderRequestHistoryQuery` carries a typed provider scope, UTC half-open interval
`[FromUtc, ToUtc)`, exact model identity, selected workload/operation/outcome/pricing
states, optional managed credential ID/issuer/subject, logical/attempt/correlation ID,
page size and optional cursor. Caller identity and database/security partition are
obtained from trusted host context, not query fields. Reject invalid enum values,
empty/reversed/oversized ranges, oversized identifiers and invalid cursor bindings.

Defaults: last 24 hours in the draft UI, 50 rows; maximum 200 rows and a 31-day interval.
This permits searching an old retained interval; it does not discard old canonical data.
Provider scope is `AllAuthorized` or `SingleProvider(ProviderIdentity)`. Single-provider
scope is fixed by its host panel and revalidated server-side; a client cannot widen it.

One query pipeline serves both tabs:

1. Authorize the metadata operation and freeze the active profile/security epoch.
2. Validate filters and cursor; derive allowed provider/partition predicates.
3. Query only the metadata index with `AsNoTracking`, scalar projection, server-side
   predicates, stable descending `SortAtUtc, EntryId` ordering and `Take(PageSize + 1)`.
   Range filters use SortAtUtc; each row exposes TimeBasis and nullable actual StartedAtUtc.
4. Return one page, next cursor, query interval, freshness/coverage and typed failure
   states. No eager owner lookup, content read, transcript/file scan or provider API call.
5. Recheck profile generation and current authorization before publishing the page. Discard
   results if the active security/profile context changed while the query awaited.
6. Details and content are separate explicit requests; each repeats authorization and the
   before-publish check. A UI cancellation flag alone is not an authorization fence.

Initial indexes:

- Unique partition + attempt ID for new attempts.
- EntryId primary key and unique partition + stable canonical source identity mapping
  for authoritative legacy identity. Source version is an optimistic-concurrency/order
  field, never a uniqueness component; replay/update preserves EntryId and SortAtUtc.
- Partition + SortAtUtc + EntryId.
- Partition + ProviderProfileId + SortAtUtc + EntryId.
- Partition + ManagedCredentialId + SortAtUtc + EntryId for per-key investigation.
- Owner source/evidence identity for replay/delete reconciliation.
- Expiry/terminal state and pending-source/retry time for bounded maintenance.

Use generated SQL and PostgreSQL query plans to decide whether a provider/model/time
index adds value. Do not index every optional filter or fetch full provider entities.
A scalar provider-name snapshot avoids a join that hydrates provider configuration.

The opaque versioned cursor binds normalized applied filters, stable partition/scope,
current transient profile generation, principal authorization revision and final sort key.
Protect it against tampering using the existing host protection mechanism. A cursor is
not authorization. On host profile/provider/security-scope or permission change, cancel,
clear results/cursors and require fresh Search. Editing a draft provider/date/model filter
in the all-provider view performs no fetch; a prior result may remain only with its applied
filter label. Pressing Search applies that draft and resets the cursor.

This is live keyset paging, not a multi-page database snapshot. There is no insertion
watermark or commit-sequence allocator. Immutable sort keys avoid repeating an already
passed row during forward navigation, but updates, retention and late backfill can change
membership. New matching rows before the current cursor require explicit Search/Refresh;
show query time and projection coverage. Previous re-queries a bounded cursor stack and
may reflect current data. There is no random page-number jump, cached copy of all pages,
or implicit unfiltered count.

## Permission Boundary

| Permission | Grants | Does not grant |
|---|---|---|
| History metadata read | Authorized bounded request metadata in the active partition/provider scope | Prompt/response, settings mutation, another server/profile, token secrets. |
| History content read | Attempt to open protected detail, subject to canonical owner/resource authorization | General transcript access or automatic access to other owner links. |
| History manage | Read/update validated global history policy and explicitly requested purge operations | Implicit permission to read every prompt. |
| Existing catalog/invoke | Existing provider operations | Any history or content access. |

Add named policy/scope constants through existing authorization catalogs. Do not hardcode
magic claim strings in providers or components. Map existing managed-token identity only
after signature/issuer/audience/lifetime and managed registry validation.

The Web authorization adapter uses `IInteractiveAccessPrincipalProvider` and existing
resource policy. It may accept an explicitly trusted local-operator principal through the
established host policy. It must not copy the Simple Chat shortcut that allows every call
when authentication is disabled. Missing authority is deny, not an empty success.

Metadata policy is intentionally an operator permission before EGCP. It does not infer
person ownership from subject equality. If a non-operator source-owned view is introduced
later, it needs a resource-filtering policy and tests; it is not assumed by this bundle.

Each content request validates entry scope, selected owner, owner existence/retention and
current source-resource permission. Metadata access, a valid entry GUID, a trace ID or a
remote request reference must not open a chat/workflow transcript. Do not scan owner
content and filter unauthorized results afterward. Return non-revealing unavailable/
denied responses and no counts from forbidden partitions.

## Caller Snapshot

Managed credential ID is the already validated registry GUID (`jti` with the managed
version), not the bearer key. Subject, issuer, credential ID, authentication kind and
bounded display-name snapshot remain separate. Legacy authenticated tokens and historical
records without a managed ID show explicit unavailable attribution. Authorization-disabled
requests use that explicit kind, not a fabricated named user.

Record a provider secret-reference ID only if required for troubleshooting and authorized;
never its value, token hash, Authorization header, connection string, signed URL, provider
configuration JSON or arbitrary HTTP headers. Access-context reference is bounded opaque
caller input, never an authorization or deduplication key. Token rotation/deletion must
not rewrite old attribution or require per-row token-registry reads.

## Detail Capture And Rendering

- Light capture contains no prompt/response excerpts. A short request log means concise
  metadata, not the first N characters of private text.
- Detailed is opt-in and history-owned only for content without a canonical content owner.
  A relay audit can be the canonical metadata owner without being a transcript owner.
  `PendingCanonical` never qualifies. Late/failed source commits leave an explicit
  owner-pending/unavailable state; do not copy their assembled prompts as a fallback.
- Adapters may capture typed current-turn input and response when their semantics are
  known. They must not guess current turn from the last user message in an arbitrary
  relay transcript. A protocol shape without a reliable typed boundary records
  `UnsupportedDetailShape` plus metadata; no hidden full-body buffering. Store bounded
  logical input once per operation/input revision and let retries reference it; responses
  remain per observed attempt. Do not copy identical assembled conversation input for each retry.
- Exclude prior conversation, system instructions, tool/RAG expansions, attachment bytes,
  audio/image base64, raw exception payloads and provider credentials from the captured
  detail contract. Existing canonical content can expose more only through its own
  authorized reader.
- Bound each field before encoding/copying where possible; truncate on valid Unicode
  boundaries and store original/captured byte counts with `Truncated`,
  `PriorContextNotCaptured`, `Redacted`, `QuotaExceeded` or `Unavailable` flags.
  Media detail is safe reference/type/size metadata, never inline binary duplication.
- Redact known configured secrets and allowlisted credential patterns before persistence.
  Do not promise universal recognition of secrets inside arbitrary user text. Detailed
  content remains sensitive, privileged, explicitly enabled and short-lived.
- Protect history-owned detail at rest using the existing persistent Data Protection
  key-ring mechanism behind a persistence adapter. Do not invent encryption. Missing
  keys produce explicit content-unavailable errors, never plaintext fallback.
- Read-only text rendering must encode untrusted content. No raw HTML, script execution,
  automatic external image loading, or unbounded Markdown rendering in details.
- Detail reads do not reprice or rewrite the historical record. Content absence is
  explained: not captured, unsupported shape, truncated, expired, owner deleted, pending
  projection, permission denied, or cryptographic key unavailable.

## General Policy Settings

Add a typed policy section to the existing settings surface, backed by a versioned
provider-history policy record per active database/security partition. Global means all
providers in that partition, not another server or user layout preferences. Existing
configuration supplies validated bootstrap defaults; persist subsequent operator changes
with optimistic concurrency and a small policy-change audit.

The policy editor shows effective Light/Detailed mode, direct/relay metadata and detail
retention, text bounds, detail quota and cleanup state only on explicit settings access.
It explains that canonical history follows its owner's retention. No expensive age/count
scan is performed to render the editor; a destructive purge/shortening preview is an
explicit bounded action. Search and applying settings are separate operations.

## Failure And Query Limits

Propagate cancellation and deadline to EF/source reads. A cancelled/stale UI request must
not replace a newer result. On validation or backend failure, keep the filter draft,
display a concise actionable error, and do not show old results as belonging to the new
filter. Preserve an explicitly labeled prior result only if its original filter is visible.

Unauthorized rows, body fields, raw provider errors and tokens must be absent from query
responses, logs and browser state. Restrict per-partition concurrent searches and impose a
10-second server deadline; do not solve overload by returning an unfiltered/partial success.
The deadline is a safety bound, not a measured response-time promise.
