# Rollout and compatibility plan

## Delivery style

Use incremental, buildable migrations. Do not perform a single repository-wide rename or delete before characterization and adapters exist.

## Compatibility windows

### Context V1 -> V2

- Keep current `AgentChatContextRegistry` publication API while introducing observation-only records and a V2 turn capture service.
- Add a V1 adapter that maps current publication/snapshot data into V2 observation records.
- New production Send path moves to V2 in SB02.
- Remove V1 invocation authority propagation after CP1/CP2 proof.

### Broad runtime -> narrow ports

- Introduce narrow ports and adapters.
- Make `IAgentRuntime`/`MafAgentRuntime` delegate only.
- Block new callers with architecture tests.
- Migrate Core, workflow, diagnostics, A2A/hosting callers.
- Delete the facade in SB18 after SB17 declares cleanup readiness and the caller scan is empty.

### Legacy session JSON -> runtime-state envelope

- Read legacy `SerializedSessionStateJson` into an envelope marked `maf/legacy-unversioned`.
- Continue only when compatibility can be proven from provider/model/history mode.
- Persist all new states as versioned envelopes.
- Do not silently overwrite incompatible legacy state.

### Single boolean approval -> per-proposal decisions

- Add a new continuation command carrying stable approval IDs.
- Keep a temporary compatibility API that maps one bool to all pending approvals.
- UI/API production callers migrate to the new command.
- Delete the compatibility API only after caller/source assertions pass.

## Feature flags

Prefer short-lived, explicit migration flags only when rollback risk requires them:

- `AgentContextCaptureV2`
- `NarrowAgentRuntimePorts`
- `VersionedMafRuntimeState`
- `DirectWorkflowLlmPort`

Each flag must have:

- owner,
- default state,
- removal subbundle,
- telemetry proving the selected path,
- no authority widening when disabled.

Do not keep dual writes or dual executions for provider/tool calls. Shadow comparison may be used only for pure deterministic mapping or validation.

## Rollback boundaries

- Context contracts can roll back before persistent V2 fields become required.
- Scope factory rollout must be atomic per execution; never combine V1 and V2 scope services in one run.
- Runtime port adapters can roll back while the broad facade delegates to them.
- Dependency reference removal rolls back only through the abstraction project, not by restoring product references in MAF.
- Process recovery rollback must not restore process code to MAF; disable the policy and fail closed instead.

## Telemetry

Add bounded telemetry for:

- context capture ID/version/transition kind/epoch (no raw context text),
- authority ID and policy fingerprint,
- observation/authority mismatch rejection,
- workspace scope identity,
- runtime adapter/schema version,
- continuation compatibility result,
- selected runtime port,
- process recovery policy result,
- workflow direct-port invocation.

Never log raw opaque attachments, secrets, full prompts, or sensitive tool arguments.

## Revision 2 stabilization phase

After SB15 and SB16, do not proceed directly to deletion. SB17 freezes feature scope, runs the cross-boundary and fault-injection matrix, repairs regressions by canonical owner, proves single-path production execution, and issues cleanup readiness. SB18 removes compatibility code only after that decision.

Model/session changes follow `claude/MODEL-FALLBACK-AND-HANDOFF.md`; durable proof, not conversation memory, is the continuity mechanism.
