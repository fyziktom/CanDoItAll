# CanDoItAll MAF Round 3 Bundle: Process Failure, Rework, and Recovery Stabilization

Date: 2026-04-27
Snapshot: actual repository ZIP uploaded after Codex completed round 2.
Scope: Microsoft Agent Framework integration, process-step failure behavior, retry/rework architecture, QA return loops, tool governance, proof reuse, and test readiness.

## Executive summary

The uploaded round 2 snapshot is significantly better than the earlier states. The basic MAF hardening work is now mostly in place:

- process automation requests `AgentFinalizerMode.Required` for governed step output;
- structured output contracts and finalizer validation exist;
- finalizer output is validated before assistant transcript persistence;
- finalizer sequence validation exists;
- tool-policy blocking has a dedicated exception type;
- provider feature matrix is more consistent than before;
- failed MAF sessions are not blindly reused as the source of truth.

The next round should not focus on another broad structured-output refactor. The main round 3 objective is to make process failure recovery efficient and safe:

> Do not repeatedly rerun the whole step when the right action is to continue from durable artifacts, QA findings, tool receipts, and a typed rework packet.

Current behavior is safe but too text-driven. The system reruns the current step with a fresh session and a generated recovery directive. This is good for avoiding poisoned chat context, but it is lossy for QA returns and partial implementation failures. The target is a typed rework/recovery engine that can finish or repair the specific delta.

## Critical issue

A real-looking OpenAI API key pattern was found in `src/CanDoItAll.Web/appsettings.json` in the uploaded ZIP. This bundle intentionally does not copy or reveal the value. Treat it as compromised: remove it from source, rotate/revoke it, and add secret scanning.

## Highest-priority round 3 findings

1. Process mutation tools are not classified as mutation tools in `AgentToolInvocationPolicyMetadata.IsMutationTool(...)`. As a result, MAF tool policy, approval filtering, and finalizer sequence validation may treat process transitions/artifact writes as read-only.
2. Recovery/retry is largely controlled by string directives and boolean decisions, not a typed `AgentReworkPacket`/`AgentRecoveryDecision`.
3. The system resets sessions on retry, which is safe, but does not yet carry enough typed context to let agents efficiently finish partial work.
4. Successful tool-name carry-forward is too coarse. Proof reuse should be based on fingerprints of files, commands, environment, and artifacts.
5. Provider approval capabilities appear too strict for Chat Completions. Official MAF documentation shows tool approvals with Azure OpenAI Chat Completion, so the matrix needs verification and possibly adjustment.
6. Manual rerun and QA repair paths are text-oriented. They should create typed rework packets and preserve minimal-delta repair semantics.
7. Domain-specific recovery guidance remains embedded in generic process dispatch code.

## Subbundles

1. `00-urgent-secret-rotation-and-secret-scanning`
2. `01-process-tool-policy-classification-and-approval`
3. `02-typed-rework-packet-and-recovery-mode-taxonomy`
4. `03-efficient-context-selection-and-session-boundary`
5. `04-qa-return-rework-loop-and-finding-propagation`
6. `05-proof-fingerprint-and-receipt-reuse`
7. `06-retry-ledger-backoff-and-loop-control`
8. `07-provider-approval-capability-proof`
9. `08-domain-recovery-guidance-provider`
10. `09-governed-output-and-finalizer-failure-boundary`
11. `10-behavioral-tests-and-doc-truthfulness`

Start with `shared-prompts/codex-master-prompt.md`.

## Execution status

Status: Implemented with targeted proof passing; full solution test command remains red on pre-existing broad-suite failures outside this round 3 recovery/governance scope.

Implementation pass completed on 2026-04-27:

- plaintext provider key material was removed from repository configuration and runtime payload copies;
- process mutation tools now classify as mutation and are approval-wrapped unless policy suppression is explicit;
- typed recovery decisions, rework packets, proof fingerprints, recovery ledger entries, context strategy, and recovery prompt rendering are implemented;
- QA/manual rerun/build/test/browser recovery paths now persist typed packet/ledger journal events;
- provider approval capability tests now reflect OpenAI/Azure OpenAI Chat Completions support for approval-required function tools;
- calculator/Blazor/project recovery guidance moved behind domain guidance providers;
- focused round 3 unit/integration regression suites pass.

See `reviews/execution-report.md` for exact commands, results, and residual risks.
