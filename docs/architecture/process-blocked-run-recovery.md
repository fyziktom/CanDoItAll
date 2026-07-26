# Process blocked-run recovery

Status: deterministic safe recovery implemented after the 2026-07-26 multiteam-development incident. Production dispatch now composes the generic coordinator, restart discovery, typed runtime-owned failures, and completed-child recovery. Ambiguous model-manager decisions remain a separate, intentionally unimplemented control plane.

## Decision

Blocked-run recovery is a runtime/application responsibility, not a prompt convention.

1. Completion gates reject invalid results and persist the exact typed diagnostics, artifacts, recovery decision, result identity, and previous-attempt summary.
2. The shared dispatch boundary invokes one blocked-run recovery coordinator for background and direct API dispatch.
3. A narrow policy catalog authorizes only bounded policies from durable typed evidence.
4. Runtime applies a receipt-bound, phase-bound rework command, appends the recovery action to the same state mutation, and redispatches the run.
5. Human attention remains mandatory for approval, policy, capability, unsupported-risk, ambiguous product decisions, and execution safety the runtime cannot neutrally prove.

Previous-attempt and diagnostic summaries are untrusted prose. They are quoted and length-limited when shown to an agent. They can explain an attempt, but cannot authorize a recovery action or substitute for an artifact.

The operational manager for safe recovery is the deterministic `IProcessBlockedRunRecoveryCoordinator`. `ProcessManagerControlLoop` is currently a dormant contract implementation: production launch plans have no manager, recovery, or resupply strategy bindings, and production composition has no control-loop ports. Until that separate control plane is wired end to end, UI narrative and terminal summaries must not be described as a running process manager.

## Incident evidence

| Run | Observed stop | Evidence-backed cause |
| --- | --- | --- |
| `52587989-a8fa-4527-a60e-65b01916bddc` | Parent implementation blocked after its child completed | The grounding validator rejected child artifact references already present in the verified forwarded-context envelope. A prior change fixed that validator. |
| `67d75d54-7814-4d1f-8c5d-364bdc75686c` | Child `prepare-solution-skeleton` blocked before implementation | The architecture artifact validly used application directory `"."`. The path resolver accepted descendants of `ProductRoot` but incorrectly rejected `ProductRoot` itself as an escape. |
| Child feature run `116f...` | Unnecessary repair attempt | A receipt-status prose regex crossed a sentence boundary from successful “test receipts” to an unrelated “storage unavailable” application condition. |
| `b25ed302-8a3a-47e7-8516-d8c1f05ac572` | Runtime-owned `create-dotnet-project` blocked twice with “Required readback path was not found” | The .NET initializer created and accepted `TetrisGame.slnx`, while template-owned required paths and readback checks retained only the requested `TetrisGame.sln`. The retry was deterministic and therefore reproduced the same contract mismatch. |
| Calculator root `a10f4c52-3135-474d-aa96-860b308d2472` | Runtime-owned restore/build exited with no diagnostic output | The app process inherited `MSBUILD_EXE_PATH` and other host-owned `dotnet watch`/MSBuild variables into child workspace commands. The same command succeeded after removing that ambient host state. |
| Calculator root `1fa528e3-8731-4d0e-b2e0-0034a5f2fa5c` | Implementation stopped in effective escalation after setup completed | DNS resolution for `api.openai.com` failed through all provider retries. Execution run `855e4fdd-0a0b-4173-80c4-eb9033b44763` durably recorded a failed terminal metric with zero tool calls, receipts, artifacts, checkpoints, approvals, structured output, and session state. That historical receipt predates the typed attestation and requires one explicit operator rework; automatic replay remains fail-closed. |
| Record `824e4161-5cce-4ef5-8f3a-acfafd11fbfb` | Cancelled-run facts failed for all five record attempts | A descendant cancellation was appended one event after its cancelled parent. The assembler treated that expected cancellation-cascade closure as an illegal newer subtree mutation. |
| Calculator record finalization | PostgreSQL logged a duplicate `PK_process_run_records` insert | An advisory-lock waiter entered a serializable snapshot before it acquired the lock, then could not observe the row inserted by the previous holder. It attempted the same key after the lock became available. |

The Tetris runs used `software-delivery` content version `2.1.51-template-path-variants`. Terra agents completed substantial work, including child build and test activity. The two primary stops above occurred in deterministic runtime code before a model could repair them. A larger model therefore increases cost without addressing the trigger.

The Calculator reproduction confirmed the same boundary under the newer models: architecture and setup agents produced valid structured work, while the deterministic child command environment caused the stop. This is not evidence of prompt overload. Context size should still be reduced, but it is a cost and reliability optimization rather than the incident root cause.

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

Runtime-owned executors now preserve the driver failure code, retry/idempotency classification, execution correlation, and a bounded safe receipt synopsis. An unclassified runtime-owned failure is explicitly unsafe/unknown rather than silently retryable. Readback failures distinguish missing paths, inaccessible paths, content mismatch, execution failure, and invalid contracts. Recovery authority remains the typed diagnostic; the synopsis is diagnostic context only.

### Child commands inherited the host build process

Workspace command execution previously copied every environment variable whose name started with `DOTNET_` or `MSBUILD`. A process hosted by `dotnet watch` and MSBuildLocator therefore passed its own bootstrap variables into an unrelated child repository. In particular, an inherited `MSBUILD_EXE_PATH` made a valid child `dotnet restore`/`dotnet build` exit with code 1 and empty output.

The command boundary now uses an explicit allow-list of stable operating-system and tool-cache variables. Host startup hooks, watch control variables, MSBuild location variables, ambient credentials, application environment names, and arbitrary prefix matches are excluded. A recipe can still supply an explicit environment overlay. Receipts persist only sorted environment-variable names, never values, so the effective boundary is auditable without leaking secrets.

### Record finalization rejected valid concurrent state

Two independent ordering defects amplified the runtime stop during summary generation.

- Record upsert took a serializable snapshot before waiting on its per-run PostgreSQL advisory lock. The transaction now uses `ReadCommitted`, so a waiter observes the prior holder’s committed insert after acquiring the lock. The unique-key recovery catch remains defense in depth, not the normal concurrency path.
- Cancellation walks the hierarchy and can append a deeper descendant cancellation immediately after the claimed parent cancellation. A cancelled record now accepts only this exact later descendant `ProcessRunCancelled` closure. Any later primary-run event, descendant reactivation, or unrelated subtree mutation still fails closed.

### Candidate paths were collapsed to one projection

The .NET solution context correctly models `.sln` and `.slnx` as alternative candidate files, and the runtime-owned scaffold executor accepts either. The template-policy binding resolved `${DotNetSolutionFileForwardSlash}` to only the requested primary path for both required-output and readback gates. On an SDK that creates `.slnx` by default, the command succeeded and every later gate still looked for `.sln`.

The .NET driver now preserves candidate semantics through its readback contract. The ordinary required-path list does not require every alternative; solution existence and membership are proven by one candidate-aware content check, while application and test project files remain ordinary required paths. The plan guard requires that one check contain the complete effective candidate set: the singular `DotNetSolutionFile` plus every `DotNetSolutionFileCandidates` entry. Runtime readback, existing-solution verification, and product completion evaluate each candidate as a whole and accept a later valid candidate after an earlier missing or stale one. Invalid or out-of-root candidates fail closed. This policy stays inside the .NET driver/template and does not enter the generic dispatcher.

### Idempotency metadata was not executable policy

The deterministic tool plans declared `current-run-repeatable`, but the runtime discarded that metadata before execution. At the same time, generic runtime-owned failures were globally classified safe and idempotent, and a failed helper could be treated as successful when optional readbacks supplied no positive evidence. This combined dead metadata with an unsafe fallback.

Operation policy is now a typed, domain-neutral deterministic-tool-plan descriptor propagated from the validated template to the owning driver. Script metadata is an optional specialization rather than the transport for retry policy. Idempotency and failed-command convergence are separate decisions. A managed helper may reconcile a failed command only when its exact operation declares `authoritative-readback-convergence`, every required readback succeeds, at least one required check provides positive evidence, and the command receipt is an ordinary failed outcome rather than denied or timed out. Missing policy on an older persisted descriptor is supported but fail-closed. Generic runtime-owned failures are unsafe and unknown unless an explicit operation policy proves repeatability.

### Relaunch lacked a current-run product baseline

Project structure intentionally describes requested work rather than preserving every prior process run. After cancellation and relaunch, a populated product root therefore reached the new architecture agent without a compact, authoritative topology observation. Guessing between conventional layouts would be unsafe and application-specific.

The .NET process now opts into a driver-local `dotnet.product-baseline/v1` launch contract. The contributor performs bounded read-only discovery of `.sln`, `.slnx`, and `.csproj` files, reports discovery completeness and full counts, samples paths within the prompt budget, and never exposes native absolute paths. A complete discovered observation proves that an existing baseline must be preserved. Its listed paths are an exact topology only when `topologySampleComplete` is true; otherwise the agent must complete bounded read-only discovery before emitting `verify-existing`. This remains a software-driver concern and does not specialize the generic dispatcher.

Structured launch contracts are now prompt-atomic in both ordinary execution and automatic recovery. A contract within the supported budget is included without whitespace rewriting or head/tail splicing; an oversized contract is omitted as one value with an explicit marker. Runtime-only deterministic descriptors are not exposed to agents. This prevents malformed JSON and reduces prompt load without teaching the dispatcher about software artifacts. Explicit presentation metadata should replace the current contract-key convention if more contract media types or priorities are introduced.

Automatic adoption of artifacts from a cancelled or failed historical run remains intentionally excluded. A safe continuation feature must require an explicit source run, validate definition/project/node/product-root lineage, bind schema-compatible artifacts to exact replacement steps, and verify stored content hashes. It must not overload subprocess root or parent identifiers, and it must never select the latest prior run implicitly.

### Blocked and escalated were conflated

A resumable blocked run was initially projected as escalated. That creates false canonical state and corrupts downstream blocked-step counts.

`ProcessRunDisposition.Blocked` is now distinct from `Escalated`. A blocked run is reportable but partial, reactivation supersedes its blocked record, and an explicit escalated runtime state remains reserved for actual escalation.

### The manager existed in instructions, not in the execution path

The template named a manager, and recovery text was added to assignments, but no production consumer invoked `ProcessManagerControlLoop` or `ProcessRecoveryDispatchHandoff`. Once a run became blocked, the dispatcher returned and nothing converted the durable decision into a rework command.

The new application coordinator closes the common deterministic path. Full model-manager composition remains a separate feature because the compiled plan still has no manager/recovery strategy bindings and the standard production resolver supports step execution only. It must not be represented as complete by adding more prompt text.

### Dispatch entry points were inconsistent

The first coordinator hook lived only in the background queue worker. `POST /runs/{id}/dispatch` called the dispatch application service directly and bypassed recovery.

Recovery now sits in `ProcessRuntimeDispatchApplicationService`, the shared boundary used by both entry points. A successful recovery returns the current active status, so stale blocked child propagation is not emitted.

The same boundary also detects a completed child’s typed parent link and offers blocked parents to recovery. Queue-worker reconciliation remains the crash/restart safety net, not the only wake-up path.

The dispatch queue retains a same-run request received while that run is still active. Post-commit and in-dispatch follow-ups use a non-blocking `EnqueueOrDefer` operation, so an active dispatch never awaits capacity from the same bounded channel it must finish before the worker can drain. Deferred requests are flushed after active dispatches release or channel capacity returns. An immediate operator redispatch takes precedence over a periodic recovery request.

Startup recovery discovery runs concurrently with queue consumption, pages blocked and terminal-child candidates with a stable `(UpdatedAtUtc DESC, RunId DESC)` keyset, and processes every page rather than a fixed 250-row prefix. Recurring discovery uses an inclusive `UpdatedAtUtc` watermark that advances to the scan start only after the whole scan succeeds. This catches runs that become blocked after startup and avoids rescanning the complete terminal-child history every 15 seconds. Rows changed during a scan remain eligible for the next scan, and exact-boundary duplicates are safe because dispatch and recovery are idempotent.

A terminal-child reconciliation failure is logged and isolated while later candidates continue. The scan then fails as a whole, so its watermark is retained and the failed child remains eligible for retry. A failed initial scan is retried as a complete initial scan; it is not incorrectly promoted to the narrower recurring scan.

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

The phase is strongly typed as `CurrentStep`, `UpstreamProducer`, `RestoredConsumer`, or `CompletedChildConsumer`. This permits one producer action followed by one restored-consumer action for the same missing-input fingerprint, and one exact completed-child consumer action, while rejecting a repeated repair under a fresh result id.

### Completed child evidence did not wake a blocked parent

Child propagation originally persisted only prose. A parent could block on a child, the child could later complete, and neither direct dispatch nor restart reconciliation had a typed, replay-safe command that could rework the parent consumer.

The child run id now flows through completion issues, adapter diagnostics, strategy diagnostics, durable receipts, and the recovery decision. Automatic parent recovery requires all of these facts:

- the decision route is `ChildRunPropagation` and the failure category is `ChildRunBlocked`;
- the decision and every source diagnostic use the exact canonical `process.adapter.subprocess_child_blocked` code, name the same child, and are normal, unrestricted, unsafe-to-retry, and idempotent;
- that exact child is completed, belongs to the same root run, and is linked to the exact parent run and step by durable launch variables;
- it is the newest linked child, every requested sibling state is returned, and every sibling is stopped (`Completed`, `Failed`, `Cancelled`, or `Blocked`);
- the command carries the child run id and exact child `UpdatedAtUtc` as its evidence version;
- the target is the blocked source step, and no matching `CompletedChildConsumer` action already exists.

Persistence validates these invariants on both write and read. A malformed completed-child action cannot be committed and then render the run unreadable after restart.

The recovery command carries a hash of the full ordered, bounded linked-child evidence set, not only the completed child row. At commit, persistence re-reads and revalidates that lineage inside a `ReadCommitted` transaction protected by a PostgreSQL advisory lock keyed by root run. A concurrent child or sibling change therefore cannot authorize a stale parent rework across application hosts.

Initial runtime state, immutable plan, and step assignments are staged in that same runtime commit. Any new run carrying an initial plan or typed parent-step precondition must supply a non-null initial-assignment collection. It must map exactly once to every executable step in both plan and state, with matching assignment run/plan/step-instance/step-key identity and matching plan/state step-definition identity. Root assignments cannot carry typed parent-run/step lineage keys. Every child assignment must carry the exact typed parent run and step from the launch precondition; commit also requires the root and parent to share the root lineage, the parent run to be active, and the parent step to be running under a live dispatch claim.

The standalone assignment store is update-only and cannot insert after launch. Its repair surface is limited to prompt, executor kind/id/display name, readiness hash, and assignment reason. Run, step, plan, roles, workflow binding, artifacts, operations, capability scope, branch gate, all launch variables, and `CreatedAtUtc` remain immutable. This closes the earlier gaps in which runtime state and lineage-bearing assignments could become visible separately or recovery repair could rewrite launch authority.

Launch cleanup follows the same fail-closed boundary. If managed-artifact initialization or a persisted activation/scheduling transition fails after the run is created, the launch service requests terminal cancellation and projection catch-up before returning the failed launch result; a cleanup failure is returned as an explicit diagnostic rather than hidden. A rejected initial commit exposes no run. Execution is queued only when execution was requested, the projected launch stage is `Running`, and the persisted runtime status is `Active`; blocked, failed, cancelled, or otherwise non-active launches are never queued.

### A transient provider failure had diagnostic evidence but no trusted replay authority

The agent adapter labeled transient provider failures safe and idempotent, but that local label did not prove that replaying the whole assigned step was safe. The recovery catalog therefore denied an implementation-step retry whose assignment could mutate an external product root. That fail-closed behavior prevented a duplicate side effect, but it also stopped the Calculator process after DNS failed before the agent executed any tool.

The adapter now distinguishes two durable diagnostic outcomes:

- `process.adapter.agent_transient_execution_retry` remains manager-required when execution detail is missing, ambiguous, or contains any possible side-effect signal;
- `process.adapter.agent_transient_execution_before_side_effects` is minted only after the adapter reloads the exact failed execution and proves a terminal failed state, zero tool calls in metrics and usage, and no receipts, artifacts, checkpoints, approvals, structured output, or serialized session state.

The second outcome includes typed execution provenance, canonical evidence hashing, exact durable-detail verification, and terminal-log chronology checks. It is still adapter-issued evidence about the adapter's own execution, not neutral proof that a previously claimed external assignment is safe to replay. It therefore cannot authorize `AgentTransientNoSideEffectsRework`. The catalog excludes this failure category from every automatic route, including the managed-artifact fallback.

Automatic authorization stays disabled until dispatch claims and attempt identities are durably recorded, a neutral application-layer verifier reads the authoritative AgentFramework ledger, and the verified evidence is atomically bound to that exact claim, attempt, and result. Until then, the attestation is diagnostic provenance for a manager or operator only. The historical Calculator DNS failure consequently needs one explicit operator rework.

This is a generic execution-safety policy. It does not know whether the product is a calculator, a C# application, a spreadsheet, an email, or a CRM record.

## Recovery policy

The coordinator orchestrates state and commands but does not own template-specific policy. `IProcessBlockedRunRecoveryPolicyCatalog` resolves these typed policies:

| Policy | Required evidence | Action |
| --- | --- | --- |
| Artifact-only safe rework | Every durable diagnostic is `SafeToRetry` and `Idempotent`, and the target assignment is restricted to managed process artifacts | Rework the typed current or upstream artifact target |
| Missing output | Exact missing-expected-output diagnostic, normal sensitivity, no restricted evidence, idempotent, typed current producer | Rework the producing step once |
| Missing input | Exact missing-required-input diagnostic, typed upstream producer, normal sensitivity, no restricted evidence, idempotent | Rework the exact producer |
| Restored input | The same upstream receipt plus currently satisfied dependencies and available required-artifact receipts | Rework the blocked consumer |
| Completed child | Exact canonical child-blocked diagnostic and child id, durable parent/step link, completed newest child state and version, complete sibling-state read, all siblings stopped | Rework the blocked parent consumer once |

These policies are process-domain neutral. Authorization comes from the durable failure category, route, lineage, sensitivity, idempotency, state version, operation contract, and budget—not from a template key. Diagnostic-local retry safety is not permission to replay a whole executor: product mutation, email, CRM, finance, deployment, and other external effects remain blocked unless a future trusted runtime contract proves whole-step replay safety. Artifact repair remains restricted to `ManagedProcessArtifactsOnly`, and every allowed operation must belong to the bounded read/write/recover managed-artifact set. Adapter-issued transient attestations are excluded even when the assignment otherwise qualifies for managed-artifact recovery. Tests use arbitrary non-software template components so a calculator-only or `simple-app-delivery` specialization cannot pass.

Automatic recovery is denied when:

- an approval key is required;
- a policy, capability, rights, or approval boundary is present;
- the runtime classifier has exhausted its retry budget;
- the same diagnostic fingerprint and recovery phase already consumed an automatic action;
- the blocked source step already consumed two automatic actions;
- the source receipt, state version, route, or responsible target changed;
- completed-child identity, version, root lineage, newest-child status, or sibling state changed;
- a linked child state is missing or any sibling is not stopped;
- the failure is a transient agent execution, including one carrying the structurally valid pre-side-effect attestation, until a trusted claim-bound verifier exists;
- the target is unrelated or not reworkable;
- no typed policy supports the durable failure facts.

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
10. Human attention remains required for governance, ambiguity, or execution safety the runtime cannot neutrally prove.
11. Alternative product projections remain alternatives from launch contract through readback.
12. Recovery policy never branches on an application topic or concrete process-template key.
13. Startup discovery pages every blocked and linked terminal-child candidate; fixed prefixes and poison rows cannot starve later runs.
14. A completed child can wake a blocked parent from direct or queued dispatch, but only the coordinator can authorize rework.
15. In-dispatch follow-ups never await their own bounded dispatch channel.
16. Unsafe automatic recovery is limited to typed managed-process-artifact operations.
17. Recovery watermarks advance only after a successful scan and never past work created during that scan.
18. An adapter-issued pre-side-effect attestation is diagnostic provenance only; it cannot authorize automatic replay of either an external-target or managed-artifact assignment.

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
- immediate and recovery follow-ups are deferred without blocking when their bounded channel is full;
- production DI resolves the coordinator and executor;
- selecting `generic-simple-local-app` selects `simple-app-delivery` and compiles a nine-step plan;
- missing-output and upstream/restored-input recovery pass with an arbitrary enterprise template key;
- `.slnx` satisfies a `.sln`/`.slnx` candidate-aware initialization contract and a missing candidate reports the attempted relative paths;
- runtime-owned failure diagnostics preserve their typed code, execution correlation, and bounded receipt synopsis;
- startup discovers all blocked runs, while recurring discovery includes only blocked runs changed at or after the previous successful scan watermark;
- recurring terminal-child discovery excludes older history and includes newly terminal children;
- more than 250 blocked and terminal-child candidates page without loss, including equal-timestamp cursor ties;
- one terminal-child reconciliation failure does not stop later child candidates or advance the recovery watermark;
- completed-child identity and version survive EF context recreation, and replay is rejected after restart;
- non-canonical child diagnostics, missing sibling rows, and every non-stopped sibling status reject completed-child recovery;
- a transient failed agent run with zero side-effect evidence receives the exact typed provenance diagnostic, while any ambiguous or nonzero evidence retains manager-required behavior;
- both exact and legacy transient diagnostics are denied automatic rework, including through the managed-artifact fallback;
- initial runtime state, immutable plan, and an exact non-null executable-step assignment mapping persist atomically; missing/mismatched mappings and invalid typed parent preconditions are rejected;
- standalone assignment insertion and non-repair mutations are rejected, while the six explicit prompt/executor/readiness/reason repair fields remain updateable;
- a post-create launch failure cancels the new run, and dispatch is queued only for an explicitly requested `Running`/`Active` launch;
- completed-child authorization revalidates the full ordered linked-child evidence under the root-run database lock before commit;
- direct completion and restart reconciliation both discover the exact blocked parent link;
- affected projects build and targeted tests pass.

After this gate, run the normal `software-delivery` process against the small Calculator project. Only after that root and its children finish without unresolved escalation should the same process be run against Tetris.

Apply the blocked-recovery-history migration only with a coordinated deployment of the new writer. Its non-null receipt sequence is intentionally not compatible with an old binary continuing to replace receipt rows.

## Remaining work

The deterministic coordinator handles common safe recovery without a model. It does not make `ProcessManagerControlLoop` production-ready. Ambiguous recovery still needs a separately designed manager decision executor, durable decision store, compiled manager/recovery bindings, and a narrow typed command catalog. The production plan must not claim those bindings until the executor and stores are actually registered and exercised.

Recovery instruction handoff is not yet atomic with runtime reactivation. The current order commits rework, then persists the repaired assignment prompt, then signals dispatch. Reversing the first two operations is unsafe because a rejected or concurrently lost rework can leave an unauthorized instruction on the assignment. A production-grade follow-up should add a typed runtime-to-assignment binding: commit the accepted binding with rework, synchronize the assignment after commit, and require dispatch to verify that exact binding before it claims the step. Active-run discovery can recover a lost queue signal, but it cannot by itself prove that the assignment instruction is current.

The intended control-plane sequence is:

1. Runtime records one typed incident and applies deterministic, policy-authorized recovery when possible.
2. An unresolved incident is offered once to the process’s compiled manager strategy with bounded evidence references, not the full execution transcript.
3. The manager may choose only from typed commands already authorized by the process plan; it cannot mutate runtime state directly.
4. A durable handoff worker applies the accepted command idempotently and acknowledges the incident.
5. Policy denial, approval requirements, or exhausted loop budgets are the only paths that require an operator.

That manager path should replace or absorb the dormant control-loop contracts rather than create a third recovery mechanism beside the coordinator and template-level manager steps.

Completed-child propagation now has a typed, durable child-recovery command. Blocked, failed, or cancelled children remain fail-closed until their own run recovers or an explicitly designed policy can prove a safe parent action. The runtime does not invent a parent rework target from prose or rework a parent while any linked sibling is non-stopped or missing from the bounded state read.

Context cost should also be reduced independently by deriving tool exposure from the current operation contract, loading large artifact bodies lazily by reference, and keeping manager packets incident-scoped. Those optimizations are worthwhile, but neither caused the Tetris stop.

Before broad or repeated expensive E2Es, add a no-model context characterization for representative simple-app steps using the exact final prompt size, tool count, and serialized tool-schema bytes. The context manifest now includes parameter JSON in its schema estimate, and warning thresholds surface the observed 55k–77k-token, 42–60-tool range; this improves observability but does not itself reduce provider-visible context.

Completed-child recovery mutations are now serialized across hosts by the root-run PostgreSQL advisory transaction described above. Root-sequence allocation in `EfProcessRuntimeEventStore` is separate remaining debt: event append still relies on a process-local lock and a database uniqueness constraint. The constraint fails safely on a collision, but a database advisory lock or persisted allocator is required before claiming efficient multi-instance event ordering.

The advisory-lock implementation still needs a real PostgreSQL contention test using independent contexts that race on one root run and proves waiter visibility plus stale-lineage rejection. Existing unit and query-translation coverage does not exercise actual PostgreSQL lock contention.
