# Process blocked-run recovery

Status: implementation and no-model architecture gate complete; coordinated deployment and a controlled model-backed end-to-end run remain.

## Decision

Blocked-run recovery is a runtime/application responsibility, not a prompt convention.

1. Completion gates reject invalid results and persist the exact typed diagnostics, artifacts, recovery decision, result identity, and previous-attempt summary.
2. The shared dispatch boundary invokes one blocked-run recovery coordinator for background and direct API dispatch.
3. A narrow policy catalog authorizes only bounded policies from durable typed evidence.
4. Runtime applies a receipt-bound, phase-bound rework command, appends the recovery action to the same state mutation, and redispatches the run.
5. Human attention remains mandatory for approval, policy, capability, unsupported-risk, and ambiguous product decisions.

Previous-attempt and diagnostic summaries are untrusted prose. They are quoted and length-limited when shown to an agent. They can explain an attempt, but cannot authorize a recovery action or substitute for an artifact.

## Incident evidence

| Run | Observed stop | Evidence-backed cause |
| --- | --- | --- |
| `52587989-a8fa-4527-a60e-65b01916bddc` | Parent implementation blocked after its child completed | The grounding validator rejected child artifact references already present in the verified forwarded-context envelope. A prior change fixed that validator. |
| `67d75d54-7814-4d1f-8c5d-364bdc75686c` | Child `prepare-solution-skeleton` blocked before implementation | The architecture artifact validly used application directory `"."`. The path resolver accepted descendants of `ProductRoot` but incorrectly rejected `ProductRoot` itself as an escape. |
| Child feature run `116f...` | Unnecessary repair attempt | A receipt-status prose regex crossed a sentence boundary from successful “test receipts” to an unrelated “storage unavailable” application condition. |

The Tetris runs used `software-delivery` content version `2.1.51-template-path-variants`. Terra agents completed substantial work, including child build and test activity. The two primary stops above occurred in deterministic runtime code before a model could repair them. A larger model therefore increases cost without addressing the trigger.

Observed calls also carried roughly 55k–77k input tokens and 42–60 tool schemas. That is a real cost and reliability concern, but it was secondary in this incident.

## Root causes

### A valid root-relative path was rejected

The path resolver compared every candidate only with `ProductRoot` plus a trailing separator. Equality with the canonical root did not pass that test. The resolver now separately accepts exact root equality while retaining the descendant check and rejection of rooted, `..`, and external paths.

### Recovery evidence was dropped

`ProcessExecutionAdapterResult.UserSafeSummary` stopped at the adapter boundary. The strategy envelope and durable result receipt did not carry it, so the manager-facing packet reported that no summary existed even when the previous executor supplied one.

The summary now flows through:

```text
execution adapter
  -> strategy result envelope
  -> runtime result receipt
  -> dedicated persistence column
  -> blocked-step packet and recovery instruction
```

`DiagnosticsJson` retains its original JSON-array contract. The summary uses a separate nullable column so rolling upgrades and legacy rows remain readable.

### Blocked and escalated were conflated

A resumable blocked run was initially projected as escalated. That creates false canonical state and corrupts downstream blocked-step counts.

`ProcessRunDisposition.Blocked` is now distinct from `Escalated`. A blocked run is reportable but partial, reactivation supersedes its blocked record, and an explicit escalated runtime state remains reserved for actual escalation.

### The manager existed in instructions, not in the execution path

The template named a manager, and recovery text was added to assignments, but no production consumer invoked `ProcessManagerControlLoop` or `ProcessRecoveryDispatchHandoff`. Once a run became blocked, the dispatcher returned and nothing converted the durable decision into a rework command.

The new application coordinator closes the common deterministic path. Full model-manager composition remains a separate feature because the compiled plan still has no manager/recovery strategy bindings and the standard production resolver supports step execution only. It must not be represented as complete by adding more prompt text.

### Dispatch entry points were inconsistent

The first coordinator hook lived only in the background queue worker. `POST /runs/{id}/dispatch` called the dispatch application service directly and bypassed recovery.

Recovery now sits in `ProcessRuntimeDispatchApplicationService`, the shared boundary used by both entry points. A successful recovery returns the current active status, so stale blocked child propagation is not emitted.

The dispatch queue also retains a same-run request received while that run is still active. The request is deferred under the same active-run lock and flushed after the active dispatch releases. An immediate operator redispatch takes precedence over a periodic recovery request.

### Canonical upstream recovery was impossible

For a missing input artifact, the recovery classifier correctly identifies the responsible upstream producer. That producer is normally already completed, while generic rework intentionally rejected completed steps.

A completed producer can now be reworked only when all of these facts match the same loaded state:

- the run is still blocked at the expected state timestamp;
- the source consumer is still an executable blocked step;
- the exact durable result idempotency key and diagnostic fingerprint match;
- the receipt route is `UpstreamStepRework`;
- the receipt names the exact producer as responsible.

Manual rework of completed steps remains rejected. Optimistic concurrency prevents a state change after validation from committing a stale recovery command.

When the producer restores the artifact, the coordinator reworks the original blocked consumer only after its typed dependencies and required artifact receipts are satisfied.

### Recovery authority was not durably ordered

EF relationship materialization does not guarantee result-receipt insertion order. Selecting the last collection item could therefore authorize an older receipt after restart.

Every result receipt now receives a monotonic `AppliedSequence` from Runtime. Persistence requires a positive, unique sequence per run, rehydrates in that sequence, and fails closed on mixed, duplicate, or nonpositive values.

The legacy migration maps each receipt to its completed claim and then to the adjacent durable step-result event. It validates claim token, result hash, applied status, correlation, actor, schema, sensitivity, causation, and `RootSequence`. The mapping must be a bijection: an unmatched or ambiguous receipt aborts the migration instead of inventing an order. Per-run sequence is assigned from the actual result-application event order, not claim creation time.

### Rework budgets were not durable actions

Counting blocked receipts is not equivalent to counting manager recovery actions. Producer rework creates no new consumer receipt, and a retry can create a new result id.

Runtime now persists a recovery-action ledger in the same optimistic-concurrency mutation as rework. Authorization is denied when any of these limits is reached:

- exact once per source blocked step, source result, phase, and target;
- once per source blocked step, diagnostic fingerprint, and phase across new result ids;
- at most two automatic actions for a source blocked step.

The phase is strongly typed as `CurrentStep`, `UpstreamProducer`, or `RestoredConsumer`. This permits one producer action followed by one restored-consumer action for the same missing-input fingerprint, while rejecting a repeated current-step repair under a fresh result id.

## Recovery policy

The coordinator orchestrates state and commands but does not own template-specific policy. `IProcessBlockedRunRecoveryPolicyCatalog` resolves these typed policies:

| Policy | Required evidence | Action |
| --- | --- | --- |
| Safe idempotent rework | Every durable diagnostic is `SafeToRetry` and `Idempotent` | Rework the typed current or upstream target |
| Simple-app missing output | `simple-app-delivery`, exact missing-expected-output diagnostic, normal sensitivity, no restricted evidence, idempotent | Rework the producing step once |
| Simple-app missing input | `simple-app-delivery`, exact missing-required-input diagnostic, typed upstream producer, normal sensitivity, no restricted evidence, idempotent | Rework the exact producer |
| Simple-app restored input | The same upstream receipt plus currently satisfied dependencies and available required-artifact receipts | Rework the blocked consumer |

Automatic recovery is denied when:

- an approval key is required;
- a policy, capability, rights, or approval boundary is present;
- the runtime classifier has exhausted its retry budget;
- the same diagnostic fingerprint and recovery phase already consumed an automatic action;
- the blocked source step already consumed two automatic actions;
- the source receipt, state version, route, or responsible target changed;
- the target is unrelated or not reworkable;
- the plan is outside a supported policy.

Per blocked step, Runtime permits at most two automatic actions and one occurrence of a diagnostic fingerprint in each phase. Durable receipt and action history are preserved across rework so those budgets survive persistence and restart.

## Simple-application lane

`simple-app-delivery` is the generic low-risk lane for small UI, Web API, console, and library applications. It is based on an explicit application profile, not topic keywords such as “Tetris” or “calculator”.

The `generic-simple-local-app` live-run profile makes that lane directly launchable. It requires the run request to declare:

- application kind and technology stack;
- approved product root and entry point;
- bounded acceptance criteria;
- only the build, test, runtime, HTTP, browser, console, or consumer proof applicable to that kind;
- any explicit unsupported-risk trigger.

It keeps one product mutation owner, independent validation, one bounded repair, and manager-contained exceptional routing. It does not silently infer browser, deployment, or privileged-operation requirements from the app topic.

## Invariants

1. A deterministic runtime defect does not consume another model call.
2. Typed diagnostic codes, classifications, and artifact receipts are the only recovery authority.
3. Previous-attempt and diagnostic-summary prose is quoted, bounded, and untrusted.
4. Every blocked result retains its receipt identity, monotonic sequence, and recovery decision.
5. Blocked and escalated remain distinct canonical states.
6. Direct and queued dispatch use the same recovery boundary.
7. A stale receipt or state version cannot authorize rework.
8. Completed-step rework is limited to an exact authorized upstream producer.
9. Phase-aware rework history is appended atomically and preserved for retry-budget enforcement.
10. Human attention is reserved for real governance or ambiguity, not missing runtime plumbing.

## Validation gate before model-backed E2E

Required no-model proof:

- exact `ProductRoot` and descendant paths pass; escapes still fail;
- receipt prose matching stays within one sentence;
- summary round-trips through persistence without changing `DiagnosticsJson`;
- malicious summary text remains quoted and cannot alter recovery instructions;
- blocked records remain blocked and are superseded on reactivation;
- direct and queued dispatch share the coordinator;
- stale, mismatched, policy-boundary, and budget-exhausted commands are rejected;
- an exact completed upstream producer can be reworked, while manual completed-step rework remains rejected;
- producer restoration can lead to consumer rework without a repeated-fingerprint loop;
- a new result id cannot reset the same source-step, fingerprint, and phase budget;
- receipt ordering and recovery actions survive an EF context restart;
- a PostgreSQL migration fixture proves result-application order wins when claim creation order is reversed;
- producer recovery, artifact restoration, consumer recovery, and replay denial survive repeated EF context recreation;
- a same-run redispatch queued during active dispatch is deferred, not dropped;
- production DI resolves the coordinator and executor;
- selecting `generic-simple-local-app` selects `simple-app-delivery` and compiles a nine-step plan;
- affected projects build and targeted tests pass.

After this gate, run one `generic-simple-local-app` scenario with a deliberately recoverable missing-output or missing-input fault. Then run one normal small application. Do not use the expensive broader `software-delivery` E2E as the first proof of this recovery architecture.

Apply the blocked-recovery-history migration only with a coordinated deployment of the new writer. Its non-null receipt sequence is intentionally not compatible with an old binary continuing to replace receipt rows.

## Remaining work

The deterministic coordinator handles common safe recovery without a model. It does not make `ProcessManagerControlLoop` production-ready. Ambiguous recovery still needs a separately designed manager decision executor, durable decision store, compiled manager/recovery bindings, and a narrow typed command catalog.

Context cost should also be reduced independently by deriving tool exposure from the current operation contract, loading large artifact bodies lazily by reference, and keeping manager packets incident-scoped. Those optimizations are worthwhile, but neither caused the Tetris stop.

Before broad or repeated expensive E2Es, add a no-model context characterization for representative simple-app steps using the exact final prompt size, tool count, and serialized tool-schema bytes including parameter JSON. Current warning thresholds and schema estimates under-report the observed 55k–77k-token, 42–60-tool calls.

Horizontal writers remain a separate persistence concern. Root-sequence allocation is currently protected by a process-local lock and a database uniqueness constraint. The constraint fails safely on a collision, but a database advisory lock or persisted allocator is required before claiming efficient multi-instance result ordering.
