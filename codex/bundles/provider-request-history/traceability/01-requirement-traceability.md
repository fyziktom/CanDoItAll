# Requirement Traceability

All in-scope implementation requirements are solved and verified by the SB01-SB09 evidence.
H01-H16 remain the invariant groups defined in the validation strategy.

| Raw note | Requirement | Final status | Implementation and proof |
|---|---|---|---|
| N001 shared use was Unpriced | R001 | Solved | Execution-time relay/SDK price evidence; provider-reported/calculated/free/unavailable provenance; SB02/SB04 and SB08 Docker proof. |
| N002 client/API key attribution, IDM later | R002/R013 | Solved | Verified managed credential ID and label are separate from subject and secret; same-subject/two-key Docker acceptance; exact-person EGCP mapping remains deferred. |
| N003 provider History tab | R003 | Solved | Shared history panel appears immediately after Sharing; SB07 component and 5032 browser proof. |
| N004 explicit request/range/filter | R004 | Solved | Lazy not-requested state, bounded range/filter validation and protected keyset paging; no history read before Search. |
| N005 Agents all-provider history | R005 | Solved | Request history tab reuses the same authorized application query across permitted providers. |
| N006 avoid duplicate tracked history | R006 | Solved | Stable canonical links retain agent/chat/workflow content at its owner; one attempt/cost row can have multiple owners. |
| N007 provider/model matching and untracked capture | R007 | Solved | Typed capture covers relay, SDK, stream, retry, batch, image, agent, simple chat and workflow paths. |
| N008 general retention settings | R008 | Solved | Versioned explicit policy Load/Apply, preview, metadata/detail retention, quota and bounded cleanup. |
| N009 Light versus Detailed | R009 | Solved | Light keeps metadata only; Detailed keeps bounded current-turn prompt/response under policy. |
| N010 avoid assembled-conversation duplication | R010 | Solved | Canonical content is linked; untracked detail stores only bounded current-turn input once per operation and response per attempt. |
| N011 use prepared parts and sound architecture | R011/R014 | Solved | Neutral three-project boundary, owner-side adapters, additive migrations, durable outbox/journal, cycle/size/async/performance audit. |
| N012 named skills and bundle-first design | R012 | Solved | Preparation, architecture review, phased execution and final bundle proof completed. |

## Authorization And Lifecycle

Metadata, content and policy permissions are independent. Trusted caller/profile/security
scope is rechecked before publishing results. Managed credentials are resolved from the active
registry; forged, revoked, expired or re-scoped identity fails closed. Canonical delete,
expiry, stale replay and late-owner behavior is covered by PostgreSQL and file-journal tests.

## Deferred Scope

Exact-person IDM/EGCP matching, cross-instance federation, historic repricing without source
evidence, full-text body search/export, exact-wire replay, prompt content-addressing, sibling
RAG instrumentation and mobile redesign remain explicit non-goals.
