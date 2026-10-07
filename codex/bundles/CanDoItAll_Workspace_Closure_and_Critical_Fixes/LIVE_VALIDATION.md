# Harness repair and remaining live proof

## No inherited budget reset

The previous campaign has used all forty reserved outbound requests. This closure is not
permission to clear that journal or silently spend another forty. A new operator instruction
may authorize a separate bounded closure campaign; otherwise the default is zero new live
requests. Preserve the historical journal and its outcome. The authorization template is a
form, not evidence that authorization occurred.

Record the authorization source, date, campaign ID, allowed provider/model, maximum total and
per-execution requests, applicable token/cost limits, and a new private journal path. Do not
put credentials in the record. The current implementation limits are 10 per execution and
40 per campaign; use the same or stricter bounds. Higher limits need a separate explicit
operator decision and are not implied by the task's size. Do not reset a proxy execution ID
to evade the per-execution bound. Failed attempts count. (R26)

Without authorization, finish all offline repairs, deterministic controls and broad testing.
Record LIVE rows as BLOCKED with reason BLOCKED_AUTHORIZATION. This is not a product failure;
it prevents a claim of complete required live verification. Do not ask repeatedly or abandon
safe work because this one precondition is missing.

## WCL-V1 — approval safety and evidence correctness

The current FileTurnAsync waits for a pending proposal and exact approval ID in the new run.
It only explicitly checks the allowed tool names before deciding. Strengthen the harness at
its actual approval boundary, not production permissions. (R25)

Validate the current typed proposal payload against the scenario's expected immutable intent:
source kind/profile/project; exact selected parent; managed relative path; overwrite=false;
expected synthetic content/hash; exact attachment source and target; and the expected tool
schema. A semantically changed payload is a new unexpected action, not a benign name match.
Use the same fixture bounds if a correctable input is legitimately resubmitted. Reject an
unexpected proposal and record a sanitized failure rather than approving it just to progress.

Add deterministic negative proposals for a sibling project, another path, overwrite=true,
a second attachment and unexpected external-target access. Assert no unintended approval,
no unexpected file/project mutation, unchanged outside canaries and no wider grant.
Keep business intent/approval IDs to prove no duplicate decision. Render and operate the real
approval control; a direct test service call is not UI approval proof.

At completion require exact owner identities and permitted effect counts, not only `Contains`
a successful tool receipt. It is acceptable to have additional harmless model read calls,
but a duplicate durable write/attachment is not erased from the report. Do not require exact
model prose as a substitute for successful tool and content evidence.

## WC-L1 — positive Project Structure file journey

Rehearse the whole production UI/runtime path with only the external provider boundary
scripted before making a paid call. This is stronger than a no-send rehearsal. Cover delayed
approval appearance, transient rendering, known write followed by blocked attachment, query
failure while writing evidence, watchdog shutdown and refusal before outbound request N+1.
Keep active run and fixture database alive until its owned evidence reads have settled.

Then, if authorized, run the current source-grounded sequence:

1. Create a unique synthetic project/container and a separate denied-scope sibling using real
   setup owners. Seed an unpredictable existing asset nonce without placing it in the read
   prompt. Give an ordinary agent exact project/storage/file capabilities through actual UI.
2. Open the selected node's Project Structure chat and inspect the admitted invocation
   snapshot/profile/source. Read the existing asset metadata and content; verify the hidden
   nonce against owner content. Do not infer file contents from titles.
3. Ask for one unique UTF-8 managed file and one attachment below the original selected node.
   Verify the actual proposal arguments, approve only that harmless intent, and capture the
   separate write/attachment acknowledgements. Inspect current tool schemas, not stale examples.
4. Require successful `project_structure_asset_get` and `project_structure_asset_content_get`
   for the returned asset; inspect exact bytes/hash through a fresh authorized owner. Reopen
   the actual file preview and page, preserving identity and unchanged sibling content.

A completed workspace_write_file followed by pending asset creation is partial success. Do
not claim no effects, retry the original whole mutating prompt against the same fixture or
recreate its prior evidence. A new live attempt uses a separately identified fresh fixture
and consumes the same campaign budget. A fixture reset is not a recovery protocol.

## WC-L2 — actual model-backed Workflow

The existing deterministic Workflow test keeps actual runtime, definition/version, project
owner and asset output, scripting only the external response. Retain this control plus the
real model proof. The live fixture currently selects nullable temperature and MaxOutputTokens
150; this is not evidence that those settings are sufficient for the configured model. (R28)
The production invoker now forwards the cap; do not remove that repair to hide a bounded
incomplete response. (R29)

Before retry, obtain or add a safe external status observer. Distinguish: request rejected
before execution; transport error; HTTP success with response failed/incomplete/cancelled;
completed response with invalid expected output; completed model with later executor/asset
failure; and missing usage/journal evidence. Capture allowlisted terminal status/reason,
request correlation, chosen safe settings and usage counts. Never dump request headers,
credentials, full system prompts, private response bodies or secret-bearing error strings.

Use controlled protocol fixtures to verify these classifications, including HTTP 200 with
non-completed model status. Correct fixture parameters only after inspecting current supported
model settings and the observed reason, within authorized request/token/cost bounds. There
must remain a failing guard for insufficient caps; do not accept partial model output as
completed merely to reach the asset node. No global temperature/max-token change is authorized.

On a live success record persisted Workflow ID/version/run, admitted origin, submitted input,
actual provider request/journal, executor trace, asset identity and byte hash. Start from the
shipped Workflow preview/run UI, verify simulation is off, view the result and actual file.
A scripted provider pass or HTTP 200 cannot clear LIVE-03.

## WC-L1/WC-L2 evidence and watchdog ownership

Use one unique artifact directory per campaign/execution/attempt. The current fixed screenshot
names in the shared fixture directory can overwrite earlier attempts; archive attempt roots
before another test and record hashes. Do not change baseline evidence in place. (R27)

Count separately: reservation admitted before sending, actual outbound attempt, successful
HTTP response, provider terminal status, model journal attempt, tool batch, proposal, approval,
owner mutation. The existing secondary watchdog counts tool-admission batches, which are not
provider requests. Record the exact trigger (batch bound, request refusal, timeout, explicit
cancellation) when stopping the host. Do not redefine a count to make a failed attempt fit.

A budget stop may terminate the owned application, not the evidence database. Complete bounded
read-back through an owned independent scope, then finalize the manifest, then dispose the
browser, database, proxy and files in their correct order. Failure capture is best-effort but
must not mask the original test exception. If evidence read fails, mark that fact and preserve
what was actually obtained. Do not reopen deleted data under the same name as "recovered" proof.

A passed manifest must be written only after required proof checks succeed, including nonzero
actual provider journal requests for live claims. Enforce final evidence mode: not-run,
rehearsal, deterministic-external-model or live. A test runner return alone says nothing about
whether the live gate ran. Budget/credential-free proxy tests must never call the network.

## CRM/HR and missing external prerequisites

The previous live CRM planner and HR reject/approve journeys passed. Shared lifetime changes
can invalidate their presentation evidence; rerun impacted live controls within the new
explicit budget, not endlessly. Carry the earlier pass as historical evidence until then.

For external shared providers, Ollama and Scenario04, inspect actual current test gates and
per-scenario prerequisites. Use existing authorized local models and private deployment
fixtures; do not download a large model or provision paid services automatically. A provider
absence is BLOCKED_ENVIRONMENT. For blocked required proof keep a precise setup manifest,
command and expected scope so the operator does not need another architectural design phase.
