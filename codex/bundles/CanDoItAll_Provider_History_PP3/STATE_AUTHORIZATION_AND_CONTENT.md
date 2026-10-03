# Query, authority and content lifetime

## State separation

Use an editor/filter identity independent of the accepted search identity. The filter draft holds
raw values and validation. A submission freezes its validated query, UTC window and scope. The
accepted result holds its query, page, coverage and cursor history. A metadata dialog belongs to
one result origin/entry; a content dialog belongs to that metadata activation and exact owner link.
No single busy Boolean can express all of those lifetimes.

Draft changes do not run Search. New Search may supersede an old read, but internal replacement is
not a user Cancel and must not briefly show the old "canceled" state. Explicit Cancel retires only
that request. Clear removes owned results and details without doing a query. Closing content does
not close an unrelated dialog; late completion never reopens closed content. Two panels/circuits
must not share mutable search state through a scoped/global singleton.

Capture action origin at render/dispatch. Compare it again after awaits for both success and error.
Clean up the exact request regardless of whether publication is still permitted; an old finally
must not clear a successor's source, loading flag or cancellation token. Canceling a UI read never
means an agent, workflow, shared relay or canonical write was undone.

## Authorization and partition

Production still uses `IProviderRequestHistory` through the existing authorized operation pipeline.
The context includes partition, execution fence, caller, allowed providers and authorization stamp;
these are authoritative data, not editable filter fields. Backend separately checks metadata,
content and canonical-owner access, and rechecks current owner/version after asynchronous work.
Keep that pipeline unchanged. [S24,S25]

A single-provider view cannot be widened by a forged field or a stale AllAuthorized event. Provider
identity alone is insufficient for profile/auth transitions. Adapters must retain the real native
context lifetime and clear all owned rows/metadata/content when that context changes. No synthetic
"current profile ID" guessed from a display label; use existing notification/access contracts.

Tests must cover an auth/profile change while a read is waiting, after metadata but before explicit
content, and after content has been displayed. Denied or stale-context responses clear the proper
sensitive data and do not present an empty success. UI fencing does not substitute for backend
refusal; public/limited callers must actually fail against the native endpoint/service.

## Content semantics and model names

All model IDs used for filtering remain exact owner identities. User-facing labels may resolve
published display names, but PP3 must not rewrite old history identity, a frozen pricing snapshot
or shared relay route. The existing Exact model identity field is not an invitation to search a
human-readable label against the wrong opaque ID. Any richer selector must be separately justified,
not incidental scope expansion.

Keep the distinction between request payload, linked conversation context, canonical run output,
redacted/truncated bounded capture and missing/expired evidence. `null` usage/cost means unavailable,
not zero. CapturedBytes and OriginalBytes must describe the supplied content honestly; do not parse
beyond a truthful capture boundary to manufacture a complete proof.

Do not fetch all transcript bytes on row render, preload all owner links, or copy content from
canonical owners into a second History database/cache. Do not put contents, raw requests, keys,
credentials, protected paths or opaque authorization cursors into URL state, telemetry or screenshots.
Only synthetic explicitly approved canary content may appear in shareable test evidence. Raw evidence
stays private, separate from safe summaries and checked for accidental credentials.

## No hidden product expansion

Search/read functionality must not run retention, exports, replay, deletion, diagnostics or model
invocations. Do not implement new history ingestion/maintenance scheduling. Capture a reproducible
recurring native failure with phase/context/performance data; fix only a bounded demonstrated defect
or create a mapped prerequisite for a larger owner change. [S05,S25]
