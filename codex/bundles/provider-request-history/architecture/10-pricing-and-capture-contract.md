# Pricing And Capture Contract

The source evidence and exact test homes are in
[sharing/pricing analysis](06-sharing-pricing-analysis.md). This file fixes implementation
decisions rather than treating every investigation alternative as an open choice.

## Pricing

The confirmed source-level defect is `SharedProviderInvocationAuditFinalizer.PersistAsync` writing
`Price: null` and unavailable pricing for both buffered and streaming relay completion.
Catalog publication/import mapping already exists; no second catalog or live price lookup
during search is needed.

Before dispatch, a small top-level `SharedProviderExecutionPricingResolver` in
ProviderManagement captures the exact profile/model tariff, model/source/publication
mapping, rate unit/currency and source revision. The finalizer receives that immutable
snapshot. Place the finalizer in its own file when extracting it; do not expand the
stream-wrapper file into a pricing/history manager.

Reuse existing `ProviderPricingCalculator` arithmetic through a cohesive validated
long-count entry point. Existing int-based callers remain compatible. Never truncate,
overflow or clamp observed long counts to create a plausible price. Extend normalized
relay usage only for categories actually observed: input, output, cached input, applicable
cache writes, reasoning, image count and supported units. Missing categories remain
nullable. Reasoning already included in output is not charged twice. Rate selection for
long context occurs per attempt, not after summing unrelated attempts.

Typed price evidence distinguishes:

| State | Meaning |
|---|---|
| ProviderReported | An existing authoritative provider-reported amount/currency is preserved without recalculating it. A reported zero is still a known amount. |
| Calculated | Observed supported units and an immutable configured tariff were sufficient. |
| ExplicitFree | Operator-configured free tariff state, with provenance. |
| PartialEstimate | Some supported usage/rates are known; missing components are explicit. |
| MissingTariff | No authoritative exact-model tariff at dispatch. |
| MissingUsage | Tariff exists, but cost-bearing usage was not observed. |
| UnsupportedUnit | Existing tariff cannot price the observed operation/unit. |
| LegacyUnavailable | Historical evidence cannot establish execution-time cost. |

Zero is a known value for authoritative ProviderReported evidence, explicit free pricing,
or valid zero observed consumption under a known tariff. Unknown is never coerced to zero.
Preserve the existing preference for provider-reported cost over a derived estimate and
its original provenance/currency. Current legacy all-zero tariff placeholders are not
silently migrated to ExplicitFree. Use an explicit tariff-kind/availability state with a
conservative migration default.

Do not sum different currencies into one amount. The request index retains currency,
cost unit, source evidence version and completeness. If the existing usage dashboard is
USD-only, non-USD amounts remain unsupported there instead of being relabeled USD.
The UI distinguishes ProviderReported cost from a configured-rate execution estimate;
neither label promises invoice reconciliation.

A canonical amount can override an attempt estimate only when it refers to that exact
attempt and matching usage granularity. A legacy/simple-chat operation aggregate may link
to several attempts for lineage/content, but its total must not be copied onto each one
or allocated by guesswork. Aggregate long-context thresholds cannot reprice those attempts.

Historical repair only uses a trustworthy persisted execution snapshot. Old null prices
remain LegacyUnavailable when no such snapshot exists. A separately requested estimate
from current rates must never overwrite original evidence or claim historic truth.

## Capture Ownership Matrix

| Existing path | New boundary and owner | Required proof |
|---|---|---|
| Buffered simple chat / test chat | Small typed chat adapter inside each existing retry dispatch; pass attempt ID to canonical invocation evidence. | Empty-result retry produces distinct attempts, one logical operation, no duplicated transcript. |
| Streaming simple chat | Typed stream adapter observes incremental usage and terminal events. | First chunk is not delayed by body buffering; cancellation retains last known usage; dispatch return true is not usage. |
| MAF SDK agent chat | Dedicated `IChatClient` history decorator inside application empty-result retry, beside transport boundary. | Production factory emits per-provider-call metadata without constructing another runtime or bypassing tool/approval policy. |
| Shared inbound relay | Existing canonical Begin/Finalize and audited stream; project that row. Lower dispatch carries trusted relay-owned marker. | Headers do not finalize; exactly one canonical relay audit and one projection, no standalone duplicate. |
| Batch item/retry/recovery | Typed item adapter allocates a new attempt for actual send while preserving job/item IDs. | Retried item has distinct attempts; recovered completed result sends/logs no new call. |
| Image generation/edit/analysis | Typed existing image/chat-vision driver seam; metadata and image count/unit evidence. | No inline image/base64 duplication, explicit unsupported price unit where applicable. |
| Speech transcription/synthesis | Typed voice driver seam; duration/units only when actually available. | Stream/file ownership unchanged; no copied audio and no guessed token cost. |
| ListModels / health / model mutation | Explicit operational/diagnostic classification around existing typed operation. | Show operational request honestly, exclude it from inference cost unless an observed child probe actually consumes a priced inference. |

The closed `AgentProviderOperationKind` includes ListModels, CompleteChat, AnalyzeImage,
GenerateImage, EditImage, TranscribeSpeech, SynthesizeSpeech, TestHealth, CreateOrUpdateModel.
No main-repository embedding path was found. Do not claim sibling RAG coverage.

A health driver can call its own chat method internally. Preserve its parent diagnostic
operation and report observed child inference if instrumentation exposes it; otherwise
mark diagnostic-operation granularity/usage unavailable. Do not invent a zero-cost or
second synthetic billable call. SDK-internal retries are similarly not separate rows
unless an existing transport observer provides evidence.

Use shared small recorder/lifecycle policies with explicit typed adapters, not a universal
`object` payload visitor. A generic handle wrapper may carry identity/queue timing but
cannot be the sole terminal/body observer. Do not alter retry, batching, tool, model
selection or cancellation policy merely to make logging easier.

## Correlation And Authority

At the publisher, managed credential/subject comes from the validated Web context. At the
consumer, preserve its local provider/import/source identity and optionally record the
publisher's existing response request-ID header. That header is correlation, not permission,
billing proof or cross-instance record uniqueness. Approved plain HTTP source links remain
observations rather than cryptographic assertions of server identity.

Do not accept caller-supplied owner markers, attempt IDs or access-context values as
authority to suppress mandatory audit. Never transfer prompts, API keys or private
credential metadata into diagnostic headers. No mandatory new OpenAI JSON/SSE fields or
wire-breaking protocol version is introduced.

## Legacy And Pending Ownership

Canonical source generations and versions are checked when owner evidence attaches.
Attachment is an idempotent compare-and-set against the same partition/request/attempt
and expected ownership reservation. Conflicting owner/provider/model facts are surfaced;
no arbitrary relabeling of a standalone request as a privileged source.

Proposed pending-owner deadline: existing maximum execution/lease deadline plus 15 minutes,
captured at dispatch. After that, an unlinked terminal attempt becomes OwnerUnavailable;
it does not capture a body or pretend to be untracked. Its orphan metadata receives the
standalone retention ceiling measured from its actual original StartedAtUtc. A late
source commit can relink an unexpired reservation after exact ownership/version validation.
If orphan metadata already expired, independently index the now-trusted retained canonical
evidence under CanonicalOwner lifetime, using its stable identity mapping. This does not
restore expired standalone metadata authority or any history-owned detail. A newer source
deletion/tombstone still wins; no deleted canonical content is resurrected. Existing
canonical evidence is not excluded merely because its commit arrived after 30 days.

An interrupted relay's existing source record and recovery remain authoritative. A
late richer terminal observation must be reconciled by a tested monotonic transition,
not overwrite concurrency checks. Recovery never repeats an inference.
