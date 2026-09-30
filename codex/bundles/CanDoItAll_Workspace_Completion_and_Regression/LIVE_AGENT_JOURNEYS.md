# Real model-backed UI journeys

These are not replaced by direct tool calls, seeded chat messages, a mock model, screenshots of an empty chat, or a passed test whose live gate returned early. Synthetic setup through actual owners is permitted; the action under test must be initiated through the shipped UI. Assert the independently observed effect as well as the UI result. [WS22, WS24]

## Preflight and existing implementation

Read all of `CrmHrLiveAgentToolUiSmokeTests.cs`, including its `LiveUiHost`, watchdog, seeded-provider configuration and evidence writer. The review inspected the first Project Structure journey and the beginning of the HR journey, not the entire helper. Reuse the established private-host pattern without relying on the ordinary app.

For the dedicated live test process only, use the current repository gates:

```text
CANDOITALL_RUN_LIVE_AGENT_VALIDATION=true
CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true
CANDOITALL_LIVE_AGENT_UI_REHEARSAL=false
```

Do not export these globally or into the broad Stable process. The first two names come from current tests; use a different provider only through a genuinely supported existing test configuration, not by spoofing OpenAI proof. Validate required credentials without printing them. A rehearsal is useful for early readiness and costs no model calls, but is not live evidence. [WS22, WS24]

Default bounds: 10 model requests per execution, 40 across these live journeys including retries, and any stricter existing token/cost limit. Use the actual provider journal/watchdog and stop before exceeding the limit. If a journey cannot finish within its bound, report the exact blocked/failed stage and continue other safe lanes; do not silently increase the budget. Record the real configured provider/model and non-secret request/usage counters.

## LIVE-01 — Project Structure context, file creation and actual read-back

### Setup and UI preparation

Create a unique synthetic project, a selected structure container and a sibling negative-control project/root. Use the same governed services and routes as the application. Pre-seed a harmless existing asset containing an unpredictable per-run nonce not supplied to the model's later read prompt. Keep expected bytes in the harness, not in the conversation. Give the fixture agent only the exact project, managed workspace and tool capabilities required. Preserve approval defaults.

Open the actual Agent settings UI, set the scoped permissions, save explicitly and read them back through the owner. Navigate to `/projects/{projectId}/structure`, wait for the actual canvas/interactive context provider, select the intended node and open the floating chat from the Project Structure surface. Observe the actual source kind, project/node and profile generation in the admitted run. No manually forged trusted source or UI-only string is sufficient.

### User-visible turns

1. Ask the agent to read the selected subtree and identify its immediate contents. Exercise the actual invocation snapshot contract. `nodeIds` means exact nodes; use `subtreeRootIds` for a branch. For notes, metadata, assets and deeper persisted facts, explicitly request `CanonicalCurrent` rather than expecting the UI snapshot to contain them. The shipped guidance makes this distinction. [WS23]
2. Ask the agent to read the seeded existing asset using the permitted asset tools and return the nonce. Do not provide that nonce in this turn. Require a successful tool receipt and matching owner content; a plausible model answer is not evidence of a read.
3. Ask it to create a small Markdown/text artifact under a unique managed relative path, containing an exact new nonce and a simple table, then attach it under the selected structure node. The shipped intended sequence is `workspace_write_file` followed by `project_structure_asset_create` with that exact `sourceWorkspacePath`. When a mutation requires approval, operate the real approval dialog for this particular harmless request. [WS23]
4. Require both `project_structure_asset_get` and `project_structure_asset_content_get` read-backs after creation. Prove matching original project/node, created asset identity and exact bytes/hash. Open the actual attachment preview/file UI and read the artifact from a fresh owner context; close/reopen the chat or page and verify the persistent result. [WS23]

Before invoking any tool, inspect its current registered schema and permissions. These exact names are source-grounded but signatures can change. Do not guess parameters from old examples. A correctable-input response is not a missing-capability verdict. Fix the input within the request budget; do not bypass the tool with direct filesystem writes and claim agent success.

### Negative controls and corroboration

Use a separate read-only or denied-scope fixture to request a harmless write or a sibling-project canary. Assert no mutation and no disclosure; reject rather than auto-escalate. At least one approval rejection must leave no created asset/file effect where the current tool's contract says none was admitted. Do not claim rollback of already admitted earlier steps. Test cancellation separately with a deterministic held provider/owner if live timing would be unreliable.

Record run ID, conversation ID, SourceKind, exact project/node/profile context, proposal/approval IDs, tool receipt outcomes, created file/asset identity and independent hash. UI proof includes streamed activity, approval controls, execution log and actual attachment preview. A terminal Completed alone is not sufficient.

A screenshot of text is not content verification. A successful `File.Exists` is not proof the file belongs to the intended project. Match identities and bytes through the owning service and the governed read path. An execution log saying a tool was invoked is not proof the owner committed it.

## LIVE-02 — CRM planning and managed HR approval

Run the existing two `CrmHrLiveAgentToolUiSmokeTests` journeys against the final source and private data, not as an unverified inherited result. The planner must be granted from the real Agent settings dialog and use the CRM planning read from Project Structure. The managed HR journey must reject once and approve once, with real owner absence/presence checks. Keep unrelated source/party counts stable. [WS22]

Read each `output/live-agent-smoke/<timestamp>/evidence.json`. Require `execution=live`, `modelRequests.used > 0`, the configured provider/model and persisted run/tool/owner facts. `not-run`, `rehearsal` and test-runner-only success cannot pass this group. Merge the two manifests as evidence of the group without counting their reruns twice. [WS24]

## LIVE-03 — Model/Workflow integration through shipped UI

Inventory the actual current model-backed Workflow executor and the agent-to-Workflow invocation path. Choose one supported harmless flow: a short model-generated text with a unique marker passed to an existing file/asset output executor. Start from the actual Workflow UI or, where shipped, request the exact saved Workflow from a scoped agent chat. Do not add an invocation capability merely for this test.

Prove the saved definition/version, submitted input, admitted source, run ID, exact executor trace, actual provider request and resulting file/asset bytes through its owner. View the run and artifact in the shipped UI. A direct `RunAsync` unit test or a mock provider belongs to another group, not LIVE-03.

Also run a deterministic version with the real runtime/owners and only the external model boundary scripted. This gives a stable baseline for triaging provider behaviour but is not live proof. No real email delivery, remote deployment, payments or external upload is allowed. If there is no safe configured model-backed Workflow path, map the absence and mark this live group BLOCKED; do not silently substitute a no-op node.

## Optional spreadsheet variation, not a replacement

When the scoped existing tools support it and the request budget remains, ask for a tiny `.xlsx` through `workspace_write_spreadsheet`, inspect it via `workspace_spreadsheet_summary` and `workspace_read_spreadsheet_range`, and attach it using the current asset contract. Preserve numeric cell/formula evidence and true artifact read-back. This is optional extra coverage; it cannot replace the required text-file journey or justify unbounded model calls. [WS23]

## Results and diagnostics

Live failure classification distinguishes provider unavailable/rate limit, model noncompliance, UI dispatch failure, authority refusal, missing configuration and product effect/reconciliation bug. Use controlled replay of the same safe scenario where possible to separate them. Never leak request secrets into diagnostic bundles. A completed non-live test cannot clear a failing live case without an explicit new successful live attempt.
