# G00 current-source decisions

This record freezes decisions before production changes; it is not executed proof. Historical preparation remains intact.

## Read ownership and identity

Keep one Module component-lifetime session and one cohesive three-read adapter over the registered workspace. Forward catalog, ordered Take=30 list and exact detail reads; no persistence/execution implementation. A history reader plus separate catalog delegate splits one test seam without reducing composition. No per-lane interfaces, circuit store, controller or event bus.

Own desired/accepted identities and independent catalog/list/detail requests. Install ownership before synchronous completion; only the current owner clears its lane. Manual selection supersedes refresh detail. Accept list before detail. Same-target failed refresh retains visibly stale rows; another target hides them. A removed selected run remains unavailable with its identity. Detail retry reads detail only. Missing explicit agent and Guid.Empty never become All; null is deliberate All. Suppress missing-target null callbacks rather than erase the page request. Accepted list makes validated agent context Ready even if detail fails.

Core missing detail currently throws an untyped exception. No text matching or gratuitous Core change: successful list absence proves unavailable; a failed detail read means safe Failed, not proven deletion. Reject wrong run/row-agent payload. No automatic fallback after disappearance.

Use captured owner tokens and Phase A proven cancellation/disposal. Test delayed registration, noncooperative success/failure/cancellation, idempotent disposal. Log lane/identities/exception type only, not raw exception objects/messages.

## Time contract

Actual application settings expose currency culture, not a user display timezone. Scheduler timezone is operational state, not a display preference. Replace LocalDateTime/ambient short-date patterns with absolute invariant UTC: yyyy-MM-dd HH:mm:ss UTC. Normalize positive/negative offsets; retain distinct DST instants. Optional absent timestamps display Not recorded. Surface receives formatted strings, no ambient services. Test en-US and cs-CZ, positive/negative offsets, and both DST sides.

## Presentation allowlist and product decisions

Only immutable copied display values, numeric counts and typed states cross the boundary. GUIDs needed for selection are permitted. Razor encodes strings; no MarkupString or arbitrary payload property.

| Input | Decision |
|---|---|
| Agent/run titles; agent/provider/model names | Bounded single-line display labels, controls removed, Razor encoded. No URL/credential expansion. |
| State/outcome/approval/risk/isolation/invocation/effect | Typed-derived labels and current badge precedence. |
| Input/result summaries | Omit arbitrary execution prose. Show state/outcome summary and explain that execution content is omitted. Truncation or generic regex is not a secret boundary. |
| Approval Details/ArgumentsJson | Omit content; show tool/kind/status/time and a fixed payload-omitted explanation. |
| Artifact path | Bounded relative segments only; reject rooted/UNC/URI/traversal/query/fragment forms as Path unavailable. No open/link effect. Preserve display name/kind/time. |
| Timeline Message | Omit arbitrary runtime text; show phase/state/time and fixed state-derived explanation. |
| Source/process/step IDs | Bounded valid GUIDs only; opaque external IDs show External reference. Preserve process-linked flag. Source kind remains a bounded label. |
| Receipt ExitSummary/RequestSummary/FailureMessage/WorkingDirectory | Omit. Show risk/invocation/effect facts. |
| Checkpoints | Kind/state/time/pending count only; no IDs/session/correlation/trace payload. |
| Metrics | Copied provider/model labels, outcome, invariant duration/token/call values and UTC. |

Default deny full domain graphs, ChatSession/messages, MetadataJson, serialized session, pending approval/arguments, structured output/schema/validation, opaque configuration/context, receipt graph, usage observations, caller/compatibility/failure-provider metadata, secrets/credentials. No object/extension dictionary. Negative tests seed synthetic sentinels into denied fields and assert absence; retained evidence excludes raw sentinel dumps. Label safety is bounded encoding, not a claim that an operator cannot type sensitive text into a display name.

This explicitly narrows free-form execution content because arbitrary provider/tool prose cannot be reliably scrubbed. Governance state, approvals, artifacts, counts and metrics remain. Runtime details retains its separate header/compatibility behavior; only its shared children adopt this message/time policy. No broad Chat refactor.

## Shared children and movement

Two actual consumers: Governance in Module and AgentRuntimeDetailsDialog in broad AgentFramework.Components. Timeline/metrics have no independent CSS. G01 immutable child display values and pure domain mapper initially use existing Models, already referenced by both consumers. G02 changes the existing children to use those values and updates both consumers; Governance Surface remains in Module. G03 moves child values/mapper and real shared Razor files into existing UI, with the proven Governance rendering closure. Broad Components and Module already reference UI. No new project, reverse dependency, copies or forwarding wrappers.

Direct consumer tests cover UTC/culture, encoded labels, omitted timeline content and metric totals. Fresh full-app pre-move baseline follows G02 closure; no movement/sandbox implementation in G00-G02.

## Tool and proof limits

CodeAnalytics snap-20260907223608-9364a937 reports no blocking errors, but its scoped empty reference list is not graph proof. Use actual MSBuild evaluation. Components MCP library/recommendation/contract/example calls returned Transport closed; inspected sibling source contracts govern preserved ListDetailShell/SelectionListItem/SectionCard/CompactStat composition. Governance needs no sibling edit.

G00 compiled discovery and semantic RED/negative adjudication remain mandatory before G01.
