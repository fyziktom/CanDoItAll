# Agent Recovery Stabilization

Round 3 adds typed recovery state for governed process automation. The dispatcher still avoids reusing failed chat sessions as truth, but retries now carry structured recovery data instead of relying only on prompt text.

## Recovery decisions

Recovery attempts use `AgentRecoveryDecision` with a typed `AgentFailureCategory` and `AgentRecoveryMode`.

- `StructuredOutputInvalid` uses `FormatRepair` and does not require a new agent execution.
- finalizer, missing-tool, critical-tool, repeated-loop, and timeout failures use a fresh step retry with durable context.
- provider failures use a provider fallback retry and a fresh session.
- QA rejections, browser proof failures, build/test failures, artifact failures, and manual reruns use a rework continuation packet.
- approval continuations stay in the same compatible session.

## Rework packets

`AgentReworkPacket` records the minimal repair contract:

- the process run and step run being repaired;
- the source execution run and optional QA step run;
- findings, target artifacts, failed tool receipts, proof requirements, and reusable proofs;
- minimal next actions and prohibited actions;
- an optional human directive.

Manual reruns and automated retries now persist `agent-rework-packet-created` journal events. Retry attempts also persist `agent-recovery-attempt-recorded` with a ledger entry.

## Proof reuse

Proof reuse is fingerprint-based. A proof receipt hashes the command, working directory, source file hashes, artifact hashes, environment summary, and tool version. Reuse is allowed only for successful, non-expired receipts whose relevant source and artifact hashes still match.

Build/test proof is invalidated by `.cs`, project, solution, props, and targets files. Browser proof is also invalidated by Razor, CSS, JavaScript, and `wwwroot` changes.

## Loop control

The recovery ledger records failure signatures, provider fallback counts, and next-attempt timestamps. The recovery worker skips active runs while a future backoff timestamp exists, and loop evaluation escalates repeated identical failures or exhausted provider fallback budgets.

## Operator control plane

Blocked, failed, refused, waiting-approval, and dead-lettered automation states are projected into the process operator control plane. Journal-backed escalations can be assigned, resolved, reopened, or converted into typed rework packets. Tool approvals are surfaced beside launch/process approvals as operator-facing approval work, and approval decisions are written to the process journal and decision ledger.

The Process Workspace Control tab combines run health, escalation queue, approval console, rework console, and attempt timeline. The timeline is the primary audit view for execution runs, approvals, dispatch health, recovery decisions, rework packets, manual reruns, and escalation state transitions.

## Tool governance

Process mutation tools are registered as mutation tools and require approval by default. Read-only process inspection tools remain read-only. Internal process mutation tools are wrapped with MAF `ApprovalRequiredAIFunction` unless approval requirements are explicitly suppressed for governed non-interactive automation.

OpenAI and Azure OpenAI Chat Completions are marked as supporting MAF approval-required function tools when function tools are enabled.

## Secret configuration

Provider API keys must not be committed to `appsettings.json`. Configure `OPENAI_API_KEY` through environment variables, user secrets, or a deployment secret provider. Any previously committed key must be rotated or revoked outside the repository.
