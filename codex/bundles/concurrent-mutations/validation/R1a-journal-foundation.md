# R1a journal foundation

Status: source authored in the managed runtime checkout on 2026-10-16; not built, discovered, executed or scanned. The main checkout remains frozen. Parent review and serialized validation are required before R1b. The authoritative sequence is the [ordered runtime checkpoints](../plan/P02b-runtime-journal-schema.md#ordered-buildable-checkpoints).

## Source scope

Eight existing production files change:

- Models `AgentToolJournalModels.cs`: separate schema-five constant, effect-only support/checkpoint/origin, typed continuation requirement and owner receipt terminal evidence. Existing factory defaults and schema-one through schema-four wire shapes remain unchanged. New source-preparation byte accounting applies only to schema five; the existing whole-journal serialized bound still applies to all versions. Owner receipt source identifiers must be nonblank and trimmed, matching the SDK restoration contract.
- Models `AgentToolProviderDispatchModels.cs`: schema five accepts existing native dispatch records while retaining their response, claim and uncertainty checks.
- Core `AgentToolJournalTransitions.cs`: immutable checkpoint kind/origin, exact saved approval bindings and immutable owner confirmation. Confirmation preserves any original SDK result and disclosure evidence; it never manufactures a provider result.
- Core `AgentToolAdmissionJournal.cs`: a confirmed owner effect cannot be claimed again.
- MAF `MafToolRunContext.cs`: effect-only and owner-confirmed checkpoints cannot enter the existing provider restart/result path.
- MAF `MafAgentExecutionAdapter.cs`, `MafAgentContinuationAdapter.cs` and `MafRuntimeAgentFactory.cs`: explicit guards reject unsupported effect-only execution before provider construction or input preparation, including a persisted effect-only journal presented with request-scoped options.

No project references, DI registrations, HTTP contracts, source authority producers or journal factories change. Execution result/API forwarding remains R1c. The existing recoverable execution read is reused; request-scoped execution and continuation perform one bounded journal read under the existing run lease. An explicit effect-only option is refused before acquiring that lease. A persisted effect-only journal disguised with request-scoped options may record a new lease identifier, but admits no segment/effect or provider call and releases the lease handle.

## Source-derived proof inventory

| Selection | Expected execution rows |
| --- | ---: |
| New `AgentToolJournalSchemaFiveTests` | 18 |
| Existing `AgentToolDisclosureEvidenceTests` | 5 |
| Existing `AgentToolBackgroundSourceSerializationTests` | 5 |
| Existing `AgentPendingApprovalCancellationTests` | 13 |
| Unit subtotal | 41 |
| New `AgentToolEffectOnlyActivationBoundaryTests` | 2 |
| Existing `AgentToolAdmissionJournalIntegrationTests` | 14 |
| Existing `AgentToolBackgroundJournalIntegrationTests`, including its reservation partial | 34 |
| Integration subtotal | 50 |
| Total | 91 |

The new Unit source contains ten Facts and two four-row InlineData theories. Its coverage includes unchanged legacy serialization and maximum retained source envelopes, schema-five checkpoint validation, exact approvals, receipt/result preservation and terminal immutability, cumulative/serialized bounds, and native provider uncertainty. The new Integration theory has two InlineData rows: real execute and continuation entry points with refusing provider seams and owned file-only journal fixtures. It checks explicit admission refusal and the persisted-journal request-scoped disguise, zero provider/effect activity, and released lease ownership. These are expected source counts, not runner results; no dynamic data participates in this selection.

Unit filter:

```text
FullyQualifiedName~AgentToolJournalSchemaFiveTests|FullyQualifiedName~AgentToolDisclosureEvidenceTests|FullyQualifiedName~AgentToolBackgroundSourceSerializationTests|FullyQualifiedName~AgentPendingApprovalCancellationTests
```

Integration filter:

```text
FullyQualifiedName~AgentToolEffectOnlyActivationBoundaryTests|FullyQualifiedName~AgentToolAdmissionJournalIntegrationTests|FullyQualifiedName~AgentToolBackgroundJournalIntegrationTests
```

Parent validation must build the changed Models/Core/MAF dependency graph and owning test assemblies in a standard configuration, confirm discovery against the source inventory, execute the focused selections and run required static gates. No test or performance pass is claimed here.

## Deliberately open boundaries

R1b adds actual effect-segment creation, lease modes and receipt reconciliation. R1c adds typed continuation forwarding; R1d enables the supported MAF branch. R2/R3 still own detached authority and producer cutover. This checkpoint supplies neither a new historical receipt permission nor runtime protection for the remaining unjournaled producers. Those boundaries remain explicit refusals or existing behavior until their ordered implementation cards are completed.
