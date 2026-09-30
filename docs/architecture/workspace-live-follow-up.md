# Live campaign follow-up

## Closure update — 2026-09-30

The [current closure report](workspace-critical-fixes-closure.md) records the repaired harness and final source pair. **No new real-model request
is authorized or issued.** The original journal remains exhausted at 40/40, with SHA-256
`f3b88f709b6d26e35a6c3792930608b3937aa77e3d230dfb2fb3eedb854712e4`.
LIVE-01, LIVE-02 and LIVE-03 are all BLOCKED_AUTHORIZATION for this source pair; the historical
LIVE-02 pass below is not transferred as a new execution.

`LiveUiHost`, `UiEvidence`, `AgentUiJourneySupport`, `ProjectFilesUiJourney` and
`ScriptedAgentUiFixture` are now top-level support types. The original CRM/HR cases remain
in `CrmHrLiveAgentToolUiSmokeTests`; Project Files, Workflow and deterministic controls
have their own classes. Typed proposal checks bind approval to original run, context,
target, path, content and overwrite/media policy. Exact delayed approval is admitted once.
The first watchdog/budget stop is recorded separately; app stop retains the database and
evidence roots until read-back and repeated cleanup finish.

The full browser inventory executes the deterministic harness controls through actual
MAF/Workflow/UI owners, including cancellation, timeout, quota refusal, queued completion,
delayed registration and retained evidence. Safe allowlisted HTTP/provider terminal metadata
is separate from reservations, batches and owner effects. Output-token forwarding remains
150 in its deterministic control, and HTTP 200 with incomplete Responses is rejected.
The earlier 150-token/incomplete explanation remains a hypothesis about the historical live
failure, not an established root cause. A later operator authorization must explicitly bind
the remaining live journeys to a provider and bounded budget; these repairs do not supply it.

## Historical campaign record

The Workspace completion campaign exhausted its authorised forty requests, including
all failed attempts. No limit was increased. LIVE-01 and LIVE-03 remain FAILED; further
live validation is blocked by the exhausted budget. LIVE-02 passed with actual planner
and HR approve/deny owner effects. These are validation findings, not an attribution of
the failures to Workspace UI extraction.

## WC-L1: positive file journey lacks complete live proof

Priority: high for application readiness. No demonstrated permission bypass, wrong-profile
effect or credential disclosure. The negative case passed: a sibling read was denied,
the asset proposal was rejected, all three original project nodes remained, and sibling
content was unchanged.

The three positive attempts consumed six, seven and nine requests. Entry commit was
`1080c24163afd3cf65fcc68756913c5dd3262a62`; checkpoints and DLL hashes are recorded in
`live-campaign-01-attempt.json`, `live-campaign-02-attempt.json` and
`live-files-03-attempt.json` beneath the ignored campaign evidence root. The final tree
checkpoint was `6e0c1859309dc3d3dbadbdfd83fd56ffbf59dd5aa2cc426c282a18d9e4b16f2b`.
Sibling inputs were unchanged; these runs used WsCompletionProof and private PostgreSQL 18.

Reproducer: run the opt-in
`Project_structure_scoped_agent_reads_creates_attaches_and_reopens_actual_file_content`
through `Invoke-Proof.ps1` with a new authorised budget journal and the owned database
fixture. It opens a selected Project Structure node, reads InvocationSnapshot context,
reads seed metadata/content, then requests workspace file write, attachment and both
read-backs through the actual chat. Never run against the user's ordinary host.

The second attempt admitted project `af7f1dfe-8864-4a50-a7d2-ae310374faff`, profile
`b57a46f8-efb5-e155-524c-3baeba5b6c87`, generation zero. Invocation context and the seed
nonce read passed. The workspace write committed at 09:56:22 UTC on September 30, 2026.
The asset-create proposal `45651513-ed9b-434e-91c4-182e17447395` was still prepared and
pending; its approval ID became available after the harness had selected it too early.
This proves retained write progress, not a completed attachment or rollback.

Call/state map:

1. `CrmHrLiveAgentToolUiSmokeTests.ProjectFiles.cs`: `FileTurnAsync` observes the actual
   `IAgentFrameworkWorkspaceService` journal and the exact approval control.
2. The MAF admission journal owns business-intent identity and approval/effect state.
   `workspace_write_file` commits the workspace file; `project_structure_asset_create`
   separately owns the governed project attachment. Cancellation is not a transaction
   spanning both writes.
3. `LiveUiHost.Watch` enforces the pre-existing batch bound and proxy refusals.
   Its former call to `PlaywrightAppFixture.DisposeAsync` also dropped its private database.
   The final attempt then lost its failure read-back with PostgreSQL `3D000`.
4. `StopOwnedApplicationAsync` now stops that process while retaining the database/root
   for evidence. Normal disposal still cleans the private fixture afterward.

The two approval-observation errors are confirmed harness defects. The final disposal
cause is a high-confidence source/lifetime inference; the exact bound that fired was
not retained. There is no equivalent pre-extraction live run and no refactor-regression
claim. The original failed records and durable reservation journal remain immutable.

Follow-up scope: fresh bounded live campaign after non-live proof, verify the exact
registered approval once, then prove all four shipped tools, node/artifact identity,
bytes/hash and reopened UI. Preserve negative controls and both bounds. Do not change
production authority, transaction protocols, schemas or introduce automatic replay.

## WC-L2: live Workflow response failed before asset proof

Priority: high for live readiness; attribution unknown between provider response and
fixture settings. The first attempt's explicit temperature zero was rejected with HTTP
400. The nullable-temperature retry retained the 150-token limit and used the separately
reproduced/repaired existing max-token handoff.

Retry checkpoint: `c265fc537d46344329468824117771c5c76bcd76267aeea00c5cd29b2e133700`.
Workflow `b5f121c4-6233-4940-a007-0fe95364365e`, version
`1e234e49-9472-4820-be30-df6d4ba05453`, project
`0107a5f3-0de6-40cf-9f53-58fe00e0ea98`, run
`ac7bc8c7-3dec-403a-ac9b-0d15a5faa8df` failed at 09:57:34 UTC. One actual request
received HTTP success; provider-history row `40e04b96-2ea7-4467-9f9d-09393292e1e8`
records Failed, with unavailable token counts. No asset success was certified.

The actual path is Workflow UI preview admission → immutable definition/version/input →
`WorkflowLlmComponentInvoker` → `ILlmInvocationPort` → Responses driver → Project Structure
executor on success. `ProviderDriverProtocol.EnsureOpenAiResponseCompleted` rejects
non-completed Responses statuses even after HTTP 200. An incomplete response under the
small output cap is plausible; its body/status reason was not captured, so it is not a
diagnosis. The scripted external-boundary control passes through the same runtime and
proves max-output tokens on the wire, an actual governed file and TestLab recording.

Follow-up first captures the sanitised terminal result and safe provider status/error
classification, without secret-bearing request/response traces. Reproduce with the
existing authorised provider and explicit bounded settings; inspect supported model
parameters before selecting a fixture value. No new provider feature or permissive
fallback is justified. If an owner/driver defect is then demonstrated, add a deterministic
response fixture before a bounded repair; otherwise classify it as provider/fixture
failure. Preserve the working deterministic runtime and all authority negative controls.

The evidence ledger hashes the original TRX, source manifests, safe owner records and
all forty reservation entries. Raw evidence is under
`artifacts/workspace-completion/20260930-1080c2416`; it is not committed. No historical
runtime comparison or lost evidence is reconstructed as fact.
