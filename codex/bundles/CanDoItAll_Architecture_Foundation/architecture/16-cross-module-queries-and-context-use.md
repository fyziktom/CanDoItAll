# 16. Cross-module queries, identity resolution, and data use

## Read direction is a first-class boundary

An agent creating a task may legitimately need CRM people, availability, a Resource document, project state, provider quotes, or workflow inputs. It queries each authoritative owner through a scoped adapter rather than reads its DbContext or scrapes its UI. Deterministic workflow/process steps use the same owner query semantics with their own trusted identity.

A published query specifies filters/record kind, bounded paging, deterministic ordering, field purpose, source/version, freshness/coverage, and safe errors. Summary, editable detail, content stream, and confidential detail are separate privileges. Use batch lookup for explicit IDs, not N+1 full-entity retrieval.

## Example: CRM person and Resource for a task

1. Establish the admitted caller's project/scope and requested task-planning purpose. Obtain the allowed CRM query operation, not the whole HR administration pack.
2. Search CRM using bounded filters. Return safe label, typed identity, permitted role/availability summary, redaction and coverage. Resolve ambiguity using IDs/disambiguation; never pick a same-name person from another scope.
3. Retrieve Resources metadata through Resources. Resolve the authorized content/version handle separately; catalog Resource is not a CRM staffing resource or an unrestricted filesystem path.
4. Before model access, apply source sensitivity and configured provider/model data-disclosure policy. Before writing the task, apply destination audience/classification and source reuse constraints. Summarization is not an automatic declassification.
5. Submit Work Management a task draft and stable typed references, not complete Party/HR records. Validate referenced resource eligibility/current revisions and target access. Reserve capacity through CRM only if the assignment policy calls for it.
6. Return the actual task receipt. Source reads never become a promise of reserved capacity or current provider execution availability.

This flow may be synchronous for bounded reads and one owner command. It does not require an outbox/saga merely to look up a name. A genuinely coordinated assignment/reservation still preserves its declared atomicity.

## Three separate permissions

**Source access:** may this actor read this entity/field now for this purpose?

**Execution disclosure:** may this selected LLM/remote tool receive these data, considering provider routing and profile? An internally hosted model can have different policy from an external provider. Architecture requires an enforceable decision at the integration boundary; it does not claim today's app already implements every residency rule.

**Destination disclosure:** may this content or summary be persisted/published to this task, project, thread, output artifact, log, or downstream run? A private HR note cannot be copied into a broadly shared task merely because the actor could read it.

Where required policy cannot be evaluated, withhold/deny the data and explain safely rather than assume permission. Do not invent a new tenancy model; bind to the actual application scope/identity infrastructure. A future configurable disclosure policy is a scoped feature with fail-closed handling, not permission to disable working authorized queries arbitrarily.

## Source trust and current validation

Business text from CRM/Resources/Memory is data, never instructions that authorize a tool or widen scope. Preserve redaction/trust metadata observed in CRM query contracts [SRC-032]. Generated labels/descriptions are equally untrusted. No document can request service-locator access, alter approval, or become a trusted actor object.

Projection reads may be stale and explicitly partial. Current owner validation is needed when a query result is used in a mutation: party inactive, deleted project, stale content, revoked access, or changed membership must be handled. Full temporal atomicity across unrelated reads is not promised; either validate the relevant precondition at commit or document an acceptable version-pinned decision.

ID references should carry owner/kind/scope and revision where needed. No identifier guessed from title, current UI selection, a path, or an LLM hallucination. NotFound/Denied masking must not leak existence through counts, pagination, timing-sensitive error detail, or a broader cache entry. Practical proof focuses on concrete product paths, not a claim of eliminating every side channel.

## Content retrieval

Metadata search does not grant full bytes. Resolve bounded stream/range/excerpt with content version/hash, permitted MIME/size and access checked at use. FileTools must respect root containment, alias binding, symlink behavior, and actual driver capabilities. A Resource URL cannot be an arbitrary network-fetch tool: use registered connector/network policy, validate destination and credentials, and apply SSRF/egress controls where network retrieval is supported.

Results disclose truncation/coverage rather than silently omit crucial sections while claiming full content. Cache by scope/purpose/source revision and policy-relevant identity. Durable checkpoints store minimum necessary references/results with appropriate retention and protection; do not automatically persist every confidential raw query response into process logs.

## Replay and headless execution

A step may retain selected references and revisions as its decision evidence. Replay does not imply permission to redisclose cached data after revocation. A later operation can verify the pinned version or request fresh data according to explicit semantics; it cannot silently swap identities. Background workers use admitted durable scope, not a current browser selection or ambient user.

Tool, workflow, and process query adapters must be independently tested for current authorization, no-source/unavailable, ambiguous identity, stale data, redaction, and content limits. A safe interactive CRM tool is not proof an uninspected process adapter preserves those rules.
