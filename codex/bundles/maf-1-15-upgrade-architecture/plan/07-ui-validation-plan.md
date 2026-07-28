# UI and Entry-Surface Validation Plan

## Scope

Browser validation must prove the behavior of each real execution entry surface without claiming that a similarly named UI element invokes MAF when it does not. Mutating checks run in an isolated Playwright lane. The final 5032 instance receives non-destructive smoke checks and remains running for user testing.

The user explicitly excluded end-to-end process execution.

## Surface Matrix

| Surface | Actual behavior to validate | Required proof | Exclusion or limitation |
|---|---|---|---|
| Agent chat | `AgentChatExecutionOrchestrator` invokes `IAgentRuntime` and the MAF adapter | normal response, multi-turn session, tool-free response, approval-required response, approve/deny continuation, visible failure | Real provider checks require a configured credential |
| Project Structure | Contextual chat enters the same agent-chat orchestrator with project context | open a project structure, invoke contextual chat, verify response/session and project context | Do not infer a separate MAF runtime |
| Workflow Editor | Preview uses `WorkflowTestRunner`; an `LlmCall` component reaches the MAF workflow LLM invoker | create/run starter preview, then validate an LLM-call workflow when credentials are available, inspect terminal output | A workflow `AgentStep` is not an agent invocation; active validation currently rejects it without an executor |
| Scheduler | The scheduler's agent action opens contextual managed-agent chat; workflow schedule targets launch workflows through Quartz | invoke the scheduler contextual chat, then configure and trigger a workflow schedule in the isolated lane and verify run/status/output | `SchedulerPlanTargetKind` contains only `Workflow` and `Process`; there is no agent schedule target, and process execution is currently unsupported |
| Process step | Published process dispatch can invoke agent/workflow adapters | unit, component, configuration, and dispatch-boundary tests only | No process E2E by user instruction |
| Recruiting | Recruiting supplies context and can attach completed run evidence | open recruiting context, invoke shell chat with that context, attach or inspect completed evidence | Recruiting does not itself launch an agent or workflow |
| A2A | Hosting maps card/message/stream/session behavior | isolated API/browser smoke and card inspection | Must use exact target preview train |

## Scenarios

### Agent chat

1. Load the agent shell and select a configured provider-backed agent.
2. Send a deterministic no-tool prompt and verify one terminal assistant response.
3. Continue the same session and verify prior context remains effective.
4. Run a governed tool request and verify the pending approval UI contains stable request IDs.
5. Deny one request and verify it does not execute.
6. In a new run, approve a request and verify it executes once.
7. Refresh or restart between pending and response where the isolated test permits it, then verify exact session restoration.
8. Verify an unavailable credential or provider produces an actionable visible error without a silent fallback.

### Workflow editor

1. At a large desktop viewport, open the workflow shell.
2. Create and run the starter preview.
3. Verify activity ordering and one authoritative terminal result.
4. When a provider credential exists, run a workflow containing an `LlmCall`.
5. Verify no duplicate execution, overlays remain usable, and the result survives reopening the run.

### Scheduler

1. Open the scheduler's contextual managed-agent chat and verify it uses the shared chat runtime.
2. Create an isolated near-term workflow schedule.
3. Trigger or wait for the scheduled fire.
4. Verify status progression, one execution, and visible output.
5. Verify unsupported process execution fails with its explicit validation message.
6. Remove only the isolated test records created by the fixture.

### Project Structure and recruiting

1. Open an isolated project structure and use its contextual chat.
2. Verify the same chat/session behavior while project context is present.
3. Open a recruiting record and use shell chat with recruiting context.
4. Inspect or attach completed execution evidence.
5. Record that recruiting is context/evidence orchestration, not a direct launch surface.

## Layout and Interaction Checks

- Use a large desktop viewport such as 1900x1200 for authoring surfaces.
- Confirm the intended scroll owner, first-viewport actions, menus, dialogs, and overlays.
- Verify controls through accessible roles/names where possible.
- Capture console errors and failed requests for every scenario.
- Do not accept DOM presence alone as proof of execution.

## Live 5032 Acceptance

After the final rebuild:

1. start the canonical web project on HTTP 5032;
2. wait for `/_dev/runtime` to report ready;
3. verify `/health`;
4. inspect the agent-framework credential status without exposing a secret;
5. perform non-destructive navigation and a safe agent/workflow smoke;
6. check recent runtime logs for new unhandled exceptions;
7. leave the managed instance running.
