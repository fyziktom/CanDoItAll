# Independent architecture review verdict

## Executive verdict

The branch is **architecturally valuable but incomplete**. The broad direction is correct and several difficult extractions were implemented well. However, the most important promise of the refactor—one canonical authority per turn—currently stops at capture/metadata. It does not yet govern the tool graph and tool invocation path. In addition, per-run workspace scope is not used by all recovery/policy readers, runtime-state compatibility has semantic defects, and the manually created workspace graph has unclear lifetime ownership.

## What was implemented correctly

1. **Narrow runtime ports are real.** `IAgentExecutionRuntime`, `IAgentContinuationRuntime`, `IProviderDiagnosticsRuntime`, and `IProviderModelAdministrationRuntime` replace the broad production interface.
2. **MAF project-reference direction improved.** The MAF project no longer directly references Modules.Security, Modules.Workspace, or Workflows.MafAdapter.
3. **Process recovery ownership moved outward.** Process artifact recovery now lives in Modules.Processes behind a generic recovery policy contract.
4. **Per-proposal Core contract exists.** Exact decision coverage is validated before continuation.
5. **Per-run capability bundles exist.** Runtime capability composition verifies the supplied `WorkspaceRuntimeServices` scope.
6. **Floating chat context model is much stronger.** Turn snapshots, transitions, epochs, detached/follow mode, and original-turn continuation leases are explicit.
7. **Lightweight LLM seam is correct.** Workflow LLM nodes use `ILlmInvocationPort` and no longer create a fake agent/session.
8. **Security contract extraction is correct.** Secret runtime contracts moved to a dedicated abstractions project.

## Why merge is still blocked

The branch has a mismatch between **declared architecture** and **effective production authority**:

```text
UI observation
  -> canonical authority record
  -> safe metadata projection
  -> runtime ignores permission fields
  -> tool providers/policy derive permissions elsewhere
```

The authority does influence the transient workspace scope, so it is neither purely decorative nor fully authoritative. This partial wiring is more dangerous than a clearly isolated compatibility path because different parts of one run can believe different policy facts.

The remaining corrective program must complete the authority cutover first, then repair scope/lifetime, state/continuation, governance, approvals, and lightweight inference. Cleanup and optional ordinary-chat work comes only after those foundations pass checkpoints.

## Architecture scorecard (1–5)

| Area | Score | Review |
|---|---:|---|
| Dependency direction | 4.0 | Major reverse references were removed; MAF still has broad concrete implementation/composition references. |
| Runtime responsibility split | 4.0 | Narrow ports and adapters are real; request contracts and composition still carry transitional breadth. |
| Canonical authority integrity | 2.0 | Authority is captured and persisted but is not the runtime permission source. |
| Workspace scope integrity | 2.5 | Main tools are scope-bound; recovery/script inspection and identity/lifetime are incomplete. |
| State/continuation integrity | 2.5 | Envelope and exact decisions exist; payload inspection/fingerprints/history semantics require repair. |
| Process isolation | 3.5 | Recovery moved correctly; process facts remain embedded in MAF tool-policy construction. |
| Approval ownership | 3.0 | Core is per-proposal; UI/API remain all-or-nothing and caches need lifecycle bounds. |
| Lightweight LLM architecture | 3.5 | Correct seam, not yet hardened for user-facing ordinary chat. |
| Independent validation | 2.5 | Extensive branch proof artifacts exist, but no GitHub checks are attached and known failures remain. |
| Overall merge readiness | 2.5 | Strong foundation, corrective bundle required. |

## Required merge blockers

Complete and pass `SB00`–`SB14`, `SB16`, and `SB17`. `SB15` is optional and may be deferred because it adds a future ordinary-chat application foundation rather than repairing an existing branch regression.
