# Structured Input

Profile: `initiative`. Current authorization: prepare and validate this bundle only.

| Input | Literal concern | Required outcome |
|---|---|---|
| N001 | Shared provider is used but shown as unpriced | Honest request-time pricing evidence and explicit unpriced reasons. |
| N002 | What client/API key used what; IDM later via EGCP | Verified credential identity and subject separately; defer exact-person mapping. |
| N003 | Another tab next to Sharing for each provider | A provider-scoped History tab. |
| N004 | Not loaded immediately; request and range/filters like Manager Summary | No history, totals, details, or historical filter lookup on mount/tab switch/filter edit. |
| N005 | Another Agents tab over all providers, also explicit load | The same history feature with an authorized all-provider scope. |
| N006 | Agent chats, simple chats, workflows, etc. already stored | Link canonical records; no second full transcript store for tracked calls. |
| N007 | Match provider ID/model and store untracked calls | Provider/model filtering with typed attempt identity; each existing invocation path covered. |
| N008 | General setting for how much history to keep | Validated retention, quotas, cleanup, and visible coverage. |
| N009 | Light log versus detailed prompt/response log | Light metadata by default; explicitly enabled bounded detail for untracked content. |
| N010 | Long conversation prompts can duplicate all previous messages | Existing content stays with its owner; untracked detail captures current-turn data, not repeated history. |
| N011 | Prepared parts exist; analyze dependencies and large files | Reuse existing stores; explicit owners, test seams, and anti-growth gates. |
| N012 | Named architecture/performance skills; prepare bundle only | Analyzed design, two-pass performance review, phases, traceability and readiness; no implementation. |

## Constraints

- The two tabs are required; they are two scopes of one search feature.
- Metadata projection is allowed only as a compact search index; no prompts/responses
  or redundant authoritative charge records in that index.
- Existing request kinds include chat, image analysis/generation/editing, speech
  transcription/synthesis and model mutation. Catalog and health operations are classified
  explicitly. No main-repository embedding path was found; sibling RAG is out of scope.
- App UI target: 1920 x 1080 desktop. No mobile redesign or shared BaseLib changes planned.
- Preserve shared-provider wire compatibility, cancellation, streaming, existing IDs,
  canonical history, provider configuration and token lifecycle.
- No live requests, builds, migrations, deletion, settings edits, or deployments in preparation.

## Design Assumptions For Review

Detailed capture means a bounded, redacted logical current turn and response, with
completeness flags; it does not promise exact wire replay or copy the full prior
conversation. Existing canonical content remains available through authorized links.
Proposed defaults are Light, 30 days of direct/relay metadata, 7 days of optional
history-owned detail, and 32 KiB UTF-8 per captured text field. Canonical metadata
projections follow their existing owner's lifetime; the 30-day default does not hide
older retained agent/chat/workflow history. These are product defaults to review, not measured
capacity or compliance rules. Exact-person IDM/EGCP mapping remains deferred.

## Completion Bar For This Turn

Another engineer can execute the bundle in dependency order without inventing identity,
storage, authorization, retention, query, UI, or validation semantics. Preparation checks
pass; implementation and runtime acceptance remain explicitly not started.
