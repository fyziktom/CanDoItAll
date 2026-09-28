# Source findings and reproducible checks

**Evidence vocabulary:** “source-observed” means a branch or predicate was read at the audited SHA. It is not a test execution or proof that this caused a user's production incident. Reproduce in the current checkout; repair confirmed defects. A finding can be closed as not defective only with caller/invariant evidence and a protective regression, not a guess.

## F01 — Approval cache does not compare the approved payload

**Priority:** high, especially across the 1.22 binding migration.  
**Source:** R09, `MafApprovalContinuationDriver.CacheMatchesDurableApprovals` and `GetCachedOrRehydratedApprovals`.

The cache predicate checks request count, `RequestId` and `CallId`; it does not compare tool name, tool kind, MCP server details or serialized arguments. Another branch returns cached approvals when the durable pending list is null/empty. This is a concrete mismatch between the cache's stated durable-authority intent and its matching criteria. The final effect also depends on caller validation and native MAF binding; do not report an unproven authorization exploit.

### Minimal reproduction specification

Create an actual driver/session fixture. Cache approval P/C for a governed function with arguments A. Supply a durable approval with the same IDs but arguments B (then vary name/kind/server separately). Invoke the matching/continuation path. The source currently permits the cached tuple to be selected based only on IDs. Also clear the durable pending list while keeping the cache, and exercise the fallback. Add legitimate unchanged payload, missing cache, eviction and reconstructed-instance controls.

### Required correction and proof

A cache must be an optimization, not independent authority. Require agreement of immutable identity/payload and ownership, or invalidate it and use a verified native restoration path. Reject contradictory/missing authority where continuation would execute a protected effect. Do not “fix” mismatch by trusting a changed durable payload against a different originally surfaced native request. Preserve batch completeness, approvals for settled history, and compatibility that is explicitly supported.

Add app-level tests under actual 1.22 showing: unchanged approval resumes; mismatched/cleared authority cannot execute; duplicate response produces at most one effect; restart and cache eviction behave identically; project/agent/policy mismatch fails closed. Keep required durable tool-admission and native session checkpoints in the same proof. If a supposedly dangerous branch is genuinely unreachable, prove its invariant and either remove dead fallback or prevent future use with a regression.

## F02 — Generic service-failure text can misclassify permanent errors as transient

**Priority:** high for recovery correctness and misleading escalation diagnostics.  
**Source:** R19, `ProcessRuntimeFailureClassifier.LooksLikeTransientAgentExecutionFailure` and `LooksLikeProviderRuntimeTransientFailure`; consumers R17/R18.

`Service request failed` is accepted as a transient marker. The same generic phrase also qualifies the provider-runtime bypass of the rights/tool-boundary guard. A string such as `Service request failed. Status: 401 (Unauthorized)` can consequently take the transient path even though the actual cause needs credential repair, not repeated execution. This predicate-level counterexample is source-observed; no production trace was provided.

### Reproduction and repair contract

Exercise actual classifier and consumer with permanent provider 400/401/403 errors, a contextually permanent 404, true 408/429/500/502/503/504 failures, timeout exceptions, network failures, and caller cancellation. Include a permissions denial containing a generic service-failure phrase. Verify case/culture stability and wrapped exceptions.

Prefer typed status/category from the provider/runtime failure boundary. A recognized permanent authorization/configuration failure must override generic textual markers. Retain a bounded, conservative textual fallback only for genuinely unstructured providers. Do not convert all 404/409 cases mechanically: use provider operation/context. Honor retry-after where supported, bounded delays and cancellation.

Test the resulting process decision and its diagnostic, not only the classifier boolean: a transient pre-effect failure can recover within budget, a permanent auth failure surfaces the real actionable cause without wasting repeated execution attempts, and cancellation is terminal cancellation rather than retry/escalation. Do not weaken the separate no-side-effect proof simply because an error is transient.

## F03 — A2A endpoint construction leaks owned resources on partial failure

**Priority:** medium; correctness and resource reliability.  
**Source:** R15, `A2ARemoteAgentToolFactory.CreateEndpointToolsAsync` and outer `CreateSkillToolsAsync`.

The endpoint method creates its `HttpClient` before card discovery. Exceptions from card resolution, an empty skill list, `AsAIAgent`, an empty allow-list result or skill-tool construction can occur before the method returns the disposables to the outer accumulator. The outer catch disposes only completed earlier endpoint results, not the current endpoint's locally owned resources. This is a source-observed ownership gap, independent of the upgrade.

### Reproduction and repair contract

Use controlled card/HTTP responses and an observable lifetime seam where necessary. Cover card-fetch exception, no skills, agent construction failure if injectable, no matching allowed skill, duplicate normalized tool name, and success followed by normal owner disposal. Include multiple endpoints so a later failure verifies cleanup of both prior and current resources. Assert cancellation also cleans up and no tool is returned with a prematurely disposed client.

Choose a compact ownership-transfer design: acquire locally, transfer only on successful return, release all currently owned resources on failure. Preserve ownership of injected/shared clients if the implementation is changed to support them; never dispose caller-owned resources. Avoid a broad DI rewrite just to test lifetime.

## F04 — Same-major runtime-state policy is not enough evidence for this migration

**Priority:** high migration requirement; **not a reproduced product failure**.  
**Source:** R11/R12/R10/R13; upstream U06–U08.

`IsAdapterPackageWithinCompatibilityRange` accepts any equal major, and also accepts missing/unparseable versions. Thus 1.20 → 1.22 passes this version predicate automatically. The rest of the fingerprints still matter, but they do not by themselves prove compatibility with changed approval semantics.

Capture genuine 1.20 native checkpoints and durable journal fixtures before changing packages. Test those fixtures under 1.22 for ordinary chat, a pending approval, settled tools, partial/unfinished batches and workflow external input. Establish explicit supported restore/migration/replay/reconciliation outcomes. An equal-major rule is acceptable only where the actual format/semantic compatibility is proven; stricter handling may be needed for governed pending work and unknown version provenance.

Do not hard-code success by altering recorded versions. Do not mark all legacy JSON safe just because it parses. Preserve valid legacy conversations where possible; fail closed with an actionable recovery path when authority/effect continuity cannot be proved. Do not introduce a universal migration engine if a small tested compatibility decision suffices.

## F05 — Maintained MAF README still describes adopted 1.15 proof

**Priority:** low, mandatory documentation accuracy.  
**Source:** R08 versus R04. The maintained adapter README still references MAF 1.15 workflow/reflection proof while dependencies are already 1.20. Update current source-truth sections for the implemented target and actual proof. Do not rewrite historical evidence as if it had run against 1.22. Check related current docs/tests for stale version assumptions and run the documentation validator.

## Investigation items — do not report these as established defects

| ID | Observation to investigate | Required distinction |
|---|---|---|
| H01 | R18's strong no-side-effect attestation rejects sessions, checkpoints, receipts and other state, even if a particular action may be read-only | Missing proof is not proof of no effects. Improve truthful evidence/classification at its owner, not by deleting negative checks. |
| H02 | R17 checks transient failed results specially, then validates response JSON | Determine whether non-transient provider failures can be mislabeled as an output-contract failure. Preserve the original typed cause if so. |
| H03 | R17 checks output-contract markers before transient text in the exception path | An exception mentioning a finalizer can mask a provider or policy cause. Trace typed exception ownership and test precedence. |
| H04 | Duplicate recovery handling in R20 constructs a decision/event while its handoff refers to stored decision identity | Inspect caller persistence/transaction semantics before alleging duplicate dispatch or an identity bug. Test exact replay identity and one budget charge. |
| H05 | R15 represents empty remote response text as “completed without text output” | Determine whether actual task status can still be pending/failed. Do not infer remote success solely from empty text or rewrite semantics without a reproducer. |
| H06 | New per-run tools interact with the admitted dynamic-tool registry in R13 | Prove schema/authority/middleware agreement after refresh and overlapping runs; absence from a global tool list is not absence from the run. |

For every confirmed additional defect in this slice, record the source, reproducer, smallest correct repair, regression and resulting UI/process behavior. Preserve explicit safeguards and document disproved hypotheses rather than leaving ambiguous “fixed” claims.
