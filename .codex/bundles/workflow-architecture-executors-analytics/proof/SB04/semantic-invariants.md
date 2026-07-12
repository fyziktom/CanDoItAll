# SB04 Runtime Lifecycle Semantic Invariants

## Authoritative incremental lifecycle

- Invariant ID: SB04-RUNTIME-LIFECYCLE
- Expected behavior: a run is visible as Running with one Started event before backend work; safe node progress is stored incrementally; one conditional transition owns the terminal state and terminal timestamp.
- Disallowed shallow implementation: persist only a backend-completed snapshot, invoke the backend after a partial initial write, discard prior progress on failure, or let late completion overwrite cancellation.
- Production assertions: runtime uses `TimeProvider`, `CreateRunWithStartedEventAsync`, `TryTransitionRunAsync`, and a singleton active-run registry. Terminal persistence uses a non-cancelled token.
- Red-team cases: initial run failure, Started-event failure, backend exception after progress, observed cancellation, ignored-token completion race, and initiating-caller cancellation all have deterministic tests without sleeps.

## Honest cancellation capability

- Invariant ID: SB04-CANCELLATION
- Expected behavior: `RequestCancellationAsync` returns a typed outcome and signals only an active backend that advertises cancellation; the run becomes Cancelled only after the backend observes cancellation or loses the completion race.
- Disallowed shallow implementation: fabricate Cancelled for an inactive/durable run, return success for a non-capable backend, or let the compatibility adapter silently return a still-running snapshot.
- Production assertions: explicit cancellation and initiating-caller cancellation are distinct. The caller token always owns its operation even when the backend declines out-of-band cancellation.

## Honest external-response resume

- Invariant ID: SB04-EXTERNAL-RESUME
- Expected behavior: production of an external request, response resume, and active cancellation are separate backend flags. A typed resume port is optional and invoked at most once after atomic response acceptance.
- Disallowed shallow implementation: mark an unsupported in-process run Completed, consume its pending request, or infer resume support only from external-request production.
- Production assertions: unsupported in-process response leaves WaitingForInput and RespondedAtUtc untouched; the resume-capable fake is called once; malformed approval input is rejected before mutation.

## Atomic store primitives

- Invariant ID: SB04-ATOMIC-STORES
- Expected behavior: initial run plus Started event commit or roll back together, terminal transitions compare the persisted state, and one external response wins.
- Disallowed shallow implementation: interface defaults that emulate atomicity with two writes, unconditional terminal SaveRun, or read-then-write response acceptance.
- Production assertions: in-memory operations share one mutation lock. PostgreSQL operations use serializable transactions and conditional `ExecuteUpdateAsync`; a forced duplicate-event failure proves the initial run is rolled back.

## Typed caller launch boundary

- Invariant ID: SB04-CALLER-LAUNCH
- Expected behavior: API, preview tests, scheduler plan runs, and project-structure nodes construct typed selection, mode, origin, actor, correlation, completion, backend, and idempotency intent, then delegate definition resolution and runtime start to `IWorkflowLaunchService`.
- Disallowed shallow implementation: direct caller use of `IWorkflowRuntimeManager.StartAsync`, duplicate caller validation, client-spoofable process lineage, synthetic pre-run Running state, or silent backend fallback.
- Production assertions: exact API versions and latest-active API selection are distinct; API actor and correlation are server-derived; preview test runs use Preview origin and mode; scheduler lineage uses the persisted plan-run identity and one stable idempotency key; project lineage uses the real project, node, agent, and session identities.

## Typed HTTP lifecycle outcomes

- Invariant ID: SB04-CALLER-HTTP-OUTCOMES
- Expected behavior: cancellation and external-response endpoints consume typed runtime outcomes and preserve conflict, unsupported, unavailable, and failed distinctions in HTTP status codes.
- Disallowed shallow implementation: return unconditional success, fabricate cancellation, or collapse backend-unavailable and resume-failed into a generic client error.
- Production assertions: focused Kestrel tests verify 404/409/422 cancellation semantics and 404/409/422/503/502 external-response mappings; spoofed source-process JSON is rejected as an unmapped member.

## Governed generic agent workflow tools

- Invariant ID: SB04-AGENT-TOOLS
- Expected behavior: a registered runtime provider exposes typed list, start, status, cancellation, and external-response tools. Start accepts only exact saved version or latest active within one workflow and delegates Production plus WaitForStopped to `IWorkflowLaunchService` with an `AgentRuntimeInvocation` origin.
- Disallowed shallow implementation: call the runtime manager directly, accept caller-selected backend or origin, invent a session/composite id, expose arbitrary legacy GUID selection, treat unsupported resume as success, rely only on self-reported metadata for mutation approval, or launch a noninteractive retry without a stable caller idempotency key.
- Production assertions: the MAF host propagates the explicit or persisted runtime session key, falling back only to the real application session identity before a provider conversation exists. Correlation prefers governed process lineage. Central `ToolCapabilityRegistry` wraps start/cancel/response unless the host suppresses approval, while list/status remain unwrapped reads. Repeated equal start input preserves the same typed idempotency key and reports `PreservedNotEnforced` honestly.

## Typed process workflow driver

- Invariant ID: SB04-PROCESS-WORKFLOW-DRIVER
- Expected behavior: a workflow executor assignment selects one workflow id plus an optional exact version, launches through `IWorkflowLaunchService`, persists the binding, and recovers only a child run with matching workflow/version and typed process-run/assignment origin.
- Disallowed shallow implementation: encode workflow identity in a generic executor string, select any active workflow, call runtime start directly, expand the unused bridge, trust an unverified launch result, relaunch while a verified child is active, infer a child from correlation text, or fabricate process artifacts/output/external resume.
- Production assertions: latest-active resolution is scoped to the selected workflow; exact selection must be active and validation-clean; launch uses Production, WaitForStopped, typed input/origin, and stable idempotency; assignment persistence includes workflow id/version/output mapping; the outer process adapter delegates before subprocess/agent paths; the role editor retains binding fields across later edits.
- Red-team cases: missing selection, inactive exact version, same-workflow API-origin spoof, missing launch origin, mismatched version, ambiguous child runs, running/waiting recovery, failed/cancelled children, missing/invalid/truncated output, unsupported process artifact contracts, invalid UI GUIDs, and a second UI save all have deterministic tests.

## Evidence Contract

- Source raw note: workflows must start consistently from project structure, scheduler, governed agent tools, and process subprocess execution.
- Failing-first test: lifecycle, caller, and process-driver failures are recorded in `bundle://proof/SB04/failing-lifecycle.txt`, `bundle://proof/SB04/failing-callers.txt`, and `bundle://proof/SB04/failing-process-workflow.txt`; validator index: `bundle://proof/SB04/transcripts/closure.txt`.
- Passing test: lifecycle, callers, agent tools, process execution, persistence, and launch idempotency pass in `bundle://proof/SB04/passing-lifecycle.txt`, `bundle://proof/SB04/passing-callers.txt`, `bundle://proof/SB04/passing-agent-tools.txt`, `bundle://proof/SB04/passing-process-workflow.txt`, and `bundle://proof/SB04/workflow-launch-idempotency.md`.
- Changed source files: `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs` (`83951355b5bb50d3bb1dc94fbe2b767183dc33cc5da06c1688c2c7e41d0e9dff`) and `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowLaunchService.cs` (`a360c2f208229f04688a044dbc9ebfca538e8a33ba37180859c6c8315c67a654`).
- Red-team negative case: concurrent duplicate launches, incompatible idempotency fingerprints, stale lease takeover, late completion after cancellation, unsupported resume, ambiguous process children, and caller-origin spoofing are rejected explicitly.
- Downstream dependency check: SB05 consumes persisted origin/terminal state, SB06 consumes run analytics, and SB07 verified the combined architecture and browser surfaces.
