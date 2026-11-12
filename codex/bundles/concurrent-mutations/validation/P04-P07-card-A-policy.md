# P04/P07 card A: neutral policy contract

Status: source authored in the managed runtime checkout on 2026-10-16. No build, discovery, execution, static scan or runtime activation is claimed. Main-checkout source and binaries remain frozen. This implements only card A of the [runtime policy plan](../plan/P04-P07-runtime-policy.md).

## Exact scope and decisions

Three new files in existing SharedKernel own the data contract, pure resolver and bounded version-one codec: `Concurrency/DecisionDataPolicy.cs`, `Concurrency/DecisionDataPolicyResolver.cs` and `Concurrency/DecisionDataPolicyCodec.cs`. There are no services, EF references, owner queries, approvals, project-reference changes or launch settings. Existing Agent Models, Process Contracts/Application and the Unit project already reference SharedKernel in their project files; no new dependency edge is introduced. An evaluated graph has not been run for this candidate.

The resolved policy and its budget have validated constructor-only properties. Nullable override fields are untrusted preferences. Resolution applies workspace, definition, step and already-authorized invocation preferences in that order. Unknown enum values and malformed allowances are rejected even if a later preference would overwrite them. The effective modes must satisfy independently supplied server owner capabilities and minimum publication freshness. A requested unsupported pause/replan is a typed rejection, never a substitution with Fail.

Preference precedence is distinct from enclosing admitted execution ceilings. Requested allowances are intersected with the owner's explicit ceiling and every supplied enclosing budget, without mutating inputs. A replan request whose effective allowance is zero rejects; it does not become a pause or fail policy. Actual consumed budget and lineage accounting remain runtime admission work, not a counter hidden in this pure resolver. Callers must supply the complete enclosing ceiling chain from trusted persisted executions. Naming an input `authorizedInvocation` does not authorize it, and constructing capabilities does not confer owner authority.

`PlanningDefault` provides StepSnapshot, RequireCurrent, PauseForReview and zero automatic replans. Work duration and basis age are required arguments; the ten/fifteen-minute values in tests are fixtures, not activated production defaults or measured owner limits. The value contract accepts finite positive TimeSpan allowances and a nonnegative Int32 count. Each actual operation must supply measured narrower owner ceilings before activation. No immutable-input age exemption or spend ceiling is offered without a corresponding execution/accounting contract. Non-replan responses require zero automatic replans; a later explicit human replan requires separate authorized admission.

The v1 codec writes a fixed ordered object with numeric closed enums and invariant constant-format duration strings, preserving full TimeSpan precision without JavaScript integer rounding. The attributed converter keeps that wire shape independent of ambient naming/enum settings. Decode rejects missing/unknown/duplicate fields, unsupported schema versions, invalid combinations and oversized input. Its 1,024-byte encoding bound is a small structural protocol bound; it is not a business-fact or provider-work quota. Existing persisted runs acquire no new fields and are never reinterpreted or backfilled by this slice.

## Source proof and open gates

`DecisionDataPolicyTests` contains exactly eight Facts, matching the plan: explicit default; deterministic precedence; owner minimum; unsupported host behavior; invalid enum; enclosing ceiling intersection; invalid/incompatible budgets; and canonical versioned roundtrip/malformed input. The last Fact includes a fixed golden string, full tick precision, ambient serializer settings and unknown-version rejection. These are source-derived expectations only.

```text
FullyQualifiedName~DecisionDataPolicyTests
```

Parent owns the standard-configuration build, eight-row discovery/execution and required static gates. This pure slice does not prove capture timing, source membership, authority, publication fencing, durable pause, restart/replan budgets or any Agent/Process/Workflow journey. Cards B through H and R1b/R1c/R1d/R2 retain those gates. Policy source revisions and resolved policy must be persisted by the later owner admission, not fabricated here.
