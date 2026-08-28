# Provider Request History And Shared Pricing

**Prepared design; implementation has not started.** This bundle turns the request into
source-grounded architecture, nine bounded implementation phases and explicit proof gates.
[Preparation validation passed](reviews/02-preparation-validation.md); product execution remains separate.

## Profile

- `initiative` — current authorization is bundle preparation only.

## Mission

Make provider use searchable by provider/model, verified caller/API credential, date and
outcome, with honest price evidence. Provide one explicitly loaded History feature in the
provider editor beside Sharing and on Agents across authorized providers. Reuse canonical
agent/chat/workflow/relay records; retain compact metadata and bounded optional detail for
otherwise untracked content.

## Outcome Contract

- Requested outcome: both History tabs, shared price evidence, client/key attribution,
  canonical linkage, general retention/detail policy and durable authorized search.
- Hard constraints: no duplicated tracked transcripts/charges, no eager history/totals/facet
  reads, no invented legacy facts, no wrong dependency direction or expanding runtime partials.
- Evidence required before product closure: actual capture/source producer paths, focused
  positive/negative tests, real PostgreSQL/lifecycle/query-plan proof and desktop1920x1080
  normal/overlay acceptance. A prepared plan is not product proof.
- Known execution prerequisites: browser runtime failed sandbox ACL setup before reading
  the user's5210 page; later component-MCP requests returned Transport closed. Source analysis
  is complete, but live deployment, product tests and runtime performance remain unverified.
- This turn changes bundle documents only. No product build/test, model call, database
  migration, settings edit, token action or deployment was performed.

## Main Findings And Decisions

The relay's existing finalizer unconditionally persists null price/unavailable pricing.
Its usage projection then shows Unpriced. Catalog/import prices already exist; the missing
connection is execution-time evidence, not a new price catalog. This is a confirmed
**source-level** defect, not a reproduction of the specific deployed row.

Keep canonical histories and add a bounded scalar metadata index with stable attempt/source
identity. Capture at actual typed paths, including MAF SDK and stream terminal boundaries.
Preserve provider-reported costs, snapshot configured rates and never reprice old unknowns
from today's catalog. Verified managed credential ID is distinct from subject; EGCP person
mapping remains deferred.

Light metadata is the default. Optional Detailed captures a bounded current turn, with input
shared across retries and response per attempt; it never copies the entire conversation.
Direct/relay metadata defaults30d, detail7d/32KiB; canonical projections follow owner lifetime.
Search defaults24h/50rows, max31-day interval/200rows, with live keyset paging and explicit
coverage. These are proposed configurable product defaults, not measured capacity claims.

## Start Here

- [Detailed current state](analysis/01-current-state.md) and [risks](analysis/02-assumptions-and-risks.md).
- [Target architecture](architecture/01-target-solution.md), [boundaries](architecture/01-csharp-boundary-map.md)
  and [allowed/forbidden dependencies](architecture/02-csharp-dependency-direction.md).
- Normative implementation contracts: [identity/storage/lifecycle](architecture/05-history-data-lifecycle.md),
  [query/security/detail](architecture/09-search-security-contract.md),
  [pricing/capture](architecture/10-pricing-and-capture-contract.md).
- Source analysis: [sharing/pricing](architecture/06-sharing-pricing-analysis.md),
  [two-pass performance/history](architecture/07-history-performance-analysis.md),
  [UI/form behavior](architecture/08-ui-search-analysis.md).
- [Phase dependency plan](plan/01-phase-plan.md), [test/proof strategy](plan/02-validation-strategy.md),
  [architecture checkpoints](plan/architecture-checkpoints.md), [traceability](traceability/01-requirement-traceability.md).
- [Preparation review](reviews/00-bundle-self-review.md),
  [architecture verdict](reviews/csharp-architecture-gate.md), [execution report](reviews/01-execution-report.md).

Normative contracts and normalized requirements control implementation. Analysis reports
preserve investigated alternatives/source context; they do not authorize optional alternatives
that the final contracts rejected. Any real contradiction reopens preparation or its owner phase.

## Recommended Execution Order

| Phase | Outcome | Proof tier |
|---|---|---|
| [SB01](subbundles/01-contracts-and-boundaries/README.md) | Contracts And Boundary Lock | Behavioral |
| [SB02](subbundles/02-shared-pricing-evidence/README.md) | Shared Pricing Evidence | Behavioral |
| [SB03](subbundles/03-history-storage-and-lifecycle/README.md) | History Storage And Lifecycle | Governed |
| [SB04](subbundles/04-invocation-capture-and-attribution/README.md) | Invocation Capture And Caller Attribution | Governed |
| [SB05](subbundles/05-canonical-linking-and-backfill/README.md) | Canonical Linking And Backfill | Governed |
| [SB06](subbundles/06-authorized-history-search/README.md) | Authorized Bounded History Search | Governed |
| [SB07](subbundles/07-history-tabs-and-policy-ui/README.md) | History Tabs And Policy UI | Behavioral |
| [SB08](subbundles/08-runtime-and-performance-proof/README.md) | Runtime And Performance Proof | Governed |
| [SB09](subbundles/09-final-closure/README.md) | Final Closure Audit | Standard |

Only SB02/SB03 may overlap after SB01, and only in disjoint files with coordinated contracts.
All execution waits for separate user authorization and the relevant entry gates.

## Dependency And Validation Map

Three small neutral history projects isolate contracts, application policy and persistence.
Existing sources/SDKs supply adapters; they never become dependencies of the neutral feature.
Workspace owns its Settings panel through neutral ports, avoiding a reverse UI-module edge.
ProviderManagement remains independent of Workspace/Web/AgentFramework UI.

The [phase map](plan/01-phase-plan.md) orders foundational price/storage/capture/canonical/
authorization gates before UI and composed proof. Cleanup activates only after deletion and
replay safety pass. One justified affected-project regression checkpoint belongs at frozen
SB08; no automatic all-solution or real-provider suite.

## Scope Limits And UI Target Policy

No exact-person IDM/EGCP matching, global remote-log federation, billing reconciliation,
full-text body search/export, full-wire replay, prompt block store, sibling RAG instrumentation
or mobile/shared component redesign. Existing shared JSON/SSE, retries, tools, approval,
canonical retention and provider secrets remain intact.

Application target: desktop1920x1080, including constrained provider pane and relevant overlays.
Use existing components/styles. General settings loads/applies separately from history Search.
Provider Save and History Search must have separate form authority.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not executed; preparation browser startup unavailable`

For continuation, read this README, the selected phase and execution report. Do not infer
product completion from a prepared status, source-only test inventory or future command.
