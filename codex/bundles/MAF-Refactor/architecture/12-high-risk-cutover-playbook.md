# High-risk cutover playbook

## Non-negotiable cutover rules

1. Exactly one selected side-effecting production path per request.
2. Pure mapping/validation may be shadow-compared; provider calls, tools, mutations, process completion, persistence, and approvals may not.
3. A workspace service bundle switches atomically per execution.
4. A turn context and authority snapshot become immutable when admitted.
5. Approval continuation uses the original run state, never the current UI.
6. Persisted state changes use expand-read/write-contract with explicit compatibility outcomes.
7. Process recovery always enters ordinary completion gates exactly once.
8. A rollback may disable behavior and fail closed; it may not restore a forbidden dependency or widen authority.

## Critical cutovers

### UI context -> turn context + authority

- Expand: add observation, transition, affinity, authority, and turn-reference contracts/readers.
- Compare: shadow only observation/transition/digest mapping.
- Switch: one send path captures and authorizes before admission.
- Observe: context ID/version/epoch/digest, authority ID/fingerprint, profile generation.
- Roll back: select complete V1 send path for new turns; never combine V1 authority with V2 context.

### Workspace services -> scope-bound bundle

- Expand: create complete bundle and identity assertions.
- Compare: pure path/policy results in tests only.
- Switch: one execution factory decision creates all services.
- Observe: scope kind/key, bundle version, service identity, execution ID.
- Roll back: select complete legacy bundle for a new execution; active runs retain their original bundle.

### Broad runtime -> narrow ports

- Expand: add ports and compatibility facade.
- Switch order: diagnostics/admin, execution, continuation, hosted/A2A, decorators/test hosts.
- Observe: selected port, adapter version, caller family.
- Roll back: facade delegates to the new implementation; do not restore business behavior to facade.

### MAF/process -> Processes-owned policy

- Expand: typed generic evidence/failure plus Processes strategies.
- Compare: pure recovery/provider-policy decision only.
- Switch: one policy selector; ordinary completion coordinator remains sole materialization/submission path.
- Observe: policy version, evidence fingerprint, gate result.
- Roll back: disable recovery and persist failure; never call old MAF recovery.

### Legacy runtime state -> envelope

- Expand: legacy reader, envelope writer, compatibility evaluator.
- Switch: new writes use envelope; reads accept both.
- Observe: adapter ID, schema/version, compatibility result; no payload logging.
- Roll back: continue reading envelopes; old runtime cannot safely overwrite them unless a registered reverse migration exists.

### Boolean approvals -> per-proposal decisions

- Expand: stable-ID command, expected pending-set fingerprint, optimistic revision.
- Switch: UI/API first, runtime continuation second.
- Observe: decision count, pending-set hash, run revision.
- Roll back: internal bool adapter may map to all exact pending proposals only while instrumented and bounded.

### Full agent workflow call -> lightweight LLM port

- Expand: provider-backed stateless port and parity tests.
- Compare: use fake/deterministic providers; do not call paid/live provider twice in production.
- Switch: ordinary workflow node only.
- Observe: invocation kind, provider/model, usage, no capability/context assembly.
- Roll back: switch the workflow caller to legacy path temporarily; never expose payload-derived workspace scope.

## Cutover readiness checklist

- [ ] Before-state characterization exists.
- [ ] New owner is directly unit-tested.
- [ ] Negative/fault path exists.
- [ ] Production selector chooses exactly one path.
- [ ] Correlation and rollback are documented.
- [ ] Persistence compatibility is explicit.
- [ ] Architecture/source/dependency guards pass.
- [ ] Legacy path has a named removal subbundle.
