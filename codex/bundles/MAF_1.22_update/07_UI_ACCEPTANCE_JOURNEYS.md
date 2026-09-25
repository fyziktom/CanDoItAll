# UI and real-provider acceptance journeys

## Gate and ownership

**Do not begin browser/UI execution until the required standard non-browser proof in document 06 is green.** Read-only UI source inspection and writing component/browser tests may happen earlier. bUnit is part of the standard automated layer; it does not replace browser proof.

Run a real application host with disposable PostgreSQL 18 and isolated workspace state. Use current component seams and UI entry points discovered from source, not assumed legacy pages. `FloatingAgentChatHost.razor` currently only renders `ConversationShellHost` [R21]. Keep behavior in its owning conversation/module state and command/query layer; do not add it to the compatibility wrapper. Read `docs/architecture/ui-component-seams.md` in the live checkout.

Existing locators include `AiAgentFlowTests`, `AgentFrameworkSimpleChatsConsolidationPlaywrightTests`, `AppSmokeTests.ProjectStructureWorkflows.cs`, and `FloatingAgentChatHostLifecycleTests` [R22/R24/R25]. They are starting points, not proof of project-chat/process behavior. The inspected AiAgentFlow tests exercise catalog/CRM projection, not a substitute for live project-agent chat.

Use the supported large-desktop viewport (the inspected catalog journey starts at 1600×1000); let current fixtures define supported variations. Wait for interactivity and actual state, not arbitrary sleeps or repeated blind clicks. Workflow preview must show `workflow-canvas-name` for the intended definition before Run; toolbar visibility alone is insufficient [R02]. Prefer supported roles/test IDs, adding small stable selectors only where needed.

## Two clearly labeled proof modes

**Deterministic integration/browser mode:** keep app, HTTP/Blazor transport, projection, runtime orchestration and persistence real. Replace only controlled external provider/network boundaries when injecting a failure or precise tool sequence. This proves regressions reliably.

**Live-provider mode:** additionally run the actual configured approved provider through the actual MAF runtime. Keep provider identity/model/configuration and a bounded spend/attempt budget in evidence; never include keys. Mocks, seeded final messages and process-mock decorators cannot satisfy this mode. If credentials/provider access are missing, report the live gate Blocked rather than substitute synthetic output.

Seed disposable projects and configuration via public UI/API/owned fixture setup. Do not mutate internal completion/approval/receipt tables to force success. Actions central to a journey must be performed through the UI; direct API readback is appropriate as an independent oracle, not a replacement for the click/send/run being tested.

## UJ01 — Project-scoped, multi-turn agent chat

Create two disposable projects with distinct unique marker assets. Open a real agent conversation from project A, ask it to inspect the authorized asset and summarize its marker, then ask a follow-up grounded in the previous exchange. Verify the correct project/source authority, actual tool receipt, message history, final answer and lack of duplicate messages. Refresh/reopen the chat and continue.

Switch to project B and verify context updates; its private marker must not leak into A. Exercise the same agent in two sessions with different allowed tools/scopes. Add a permitted small artifact mutation in A and verify the persisted artifact via independent readback. A text answer claiming a write is not proof that the correct project was modified.

**Possible necessary UI work:** context label/readback, stale contribution invalidation, honest restore failure, non-duplicated streaming/terminal state. No broad catalog redesign.

## UJ02 — Approve, reject, resume and restart

Use a tool that genuinely requires approval under the existing policy. Verify the UI presents the exact tool/target/arguments safe for display and the correct pending state. Approve once, observe one effect and the durable consumed decision. Repeat with rejection: no effect. Double-click/replay the same UI action and verify idempotence.

With a separate pending run, stop/restart the disposable host, reopen the conversation and continue from persisted state. Include a genuine old-version checkpoint from the compatibility test. An incompatible/corrupt/missing checkpoint must not be “repaired” by synthetic consent; UI should offer the legitimate reconciliation/re-approval path and preserve history. Exercise a mixed approval batch, stale decision, cancellation while waiting, and a changed project/policy boundary.

**Possible necessary UI work:** distinguish pending, decided, expired/incompatible and already consumed; prevent contradictory actions while dispatch is in progress; show safe diagnostic/correlation. UI-disabled buttons are not the authorization boundary: backend rejection must also be tested.

## UJ03 — Simple workflow and multi-turn continuation

Use a small supported workflow with input, a deterministic transform or approved LLM step, and a declared terminal output. Create/select/save it through the current workflow UI. Confirm the correct canvas name, run it, and verify input, node progress, final artifact/output and durable run record.

Add a conditional route or handoff that requires a follow-up user turn. Test failure and cancellation without an empty “successful” terminal result. Cover pause/external input/restart if supported by the current workflow definition. A preview simulation alone is not proof that the production workflow executes.

**Possible necessary UI work:** terminal versus intermediate output, error/cancellation mapping, input readiness, route state and resumed progress.

## UJ04 — Simple agent-backed process

Launch an existing appropriate small process, or create a disposable minimal definition through supported APIs/UI. Bind a compatible agent and the exact required tools/capabilities. Run the bounded note/artifact task described as P01 in document 05. Observe step transitions, actual execution, validated finalizer, current-run artifact and process completion. No human escalation should be required for this fully provisioned normal task.

Reopen the process details and verify stable run/step identity, final output and links to real artifacts. Record every intermediate manager/recovery event separately from actual human intervention. Do not bypass readiness or mark the task completed in storage.

## UJ05 — Recoverable failure versus justified escalation

Inject one known pre-effect transient provider failure, then permit success. Through the UI observe bounded automatic recovery, the eventual valid result and no duplicate effect. Separately inject a permanent auth failure and a genuine permission denial: the UI must identify the actual problem, not endlessly claim a transient/finalizer retry. Exercise an exhausted budget to prove escalation still works when warranted.

Use a provider-boundary simulator for deterministic injection; then run a normal equivalent process with the real provider. Do not wait for an uncontrolled live outage as the only retry test. Include an unknown-effect case: its UI must show reconciliation rather than authorize unsafe repetition.

**Possible necessary UI work:** distinguish automatically recovering, awaiting approval, human escalation, failed, canceled and completed; display attempt/cause safely without exposing provider payloads or stack traces.

## UJ06 — Workflow inside a process; existing child work

If current supported definitions allow workflow-backed steps, run one through the process UI. Verify that workflow terminal output satisfies the process completion contract and that workflow failure/cancellation is preserved. Exercise parent restart with an existing pending/completed child and verify no duplicate child creation. The audited executor already contains a workflow-backed dispatch branch [R17], so a missing convenient fixture is not evidence that the whole integration is unsupported. Its automated P07 contract remains required. If a particular UI subtype is genuinely unsupported in the execution checkout, document that exact source-backed limitation and test the supported process/workflow paths separately rather than invent a UI feature outside scope.

## UJ07 — Cancellation, navigation and host lifetime

Cancel during streaming, tool wait, approval wait and an executing process where safe. Confirm terminal cancellation, no late fake success, no automatic redispatch, and released owned resources. Close/navigate away from the floating chat and reopen it; no duplicate subscriptions/messages or spurious cancellation of work meant to survive navigation. Use current behavior contracts to distinguish view disposal from an explicit user cancellation.

## UJ08 — Existing simple LLM chat regression

Exercise a simple non-tool chat through the consolidated conversation shell as a compatibility regression. It must still stream and persist history correctly and must not accidentally gain agent tools or approval machinery. This does **not** substitute for UJ01's real project-agent execution.

## Evidence for every journey

Record build/source and dependency provenance, platform/browser, viewport, fixture/project IDs, provider mode (deterministic/live), steps performed, expected versus observed outcome, run/execution/approval IDs, authoritative state/artifact readback, screenshot/trace paths and redacted console/server errors. Capture the first failure before a retry; report retries and their cause. A page screenshot, a green HTTP status, or an agent's own “done” sentence is insufficient proof.

At minimum live-provider proof must cover UJ01, UJ03's supported runtime workflow and UJ04; use safe bounded tooling. Perform browser suites on both requested platforms after their non-browser gates. For live-provider journeys record at least one actual-host run on each requested platform where the same supported provider path is available; missing access remains a clearly stated acceptance gap, not inferred cross-platform success.
