# Post-refactor debugging and bugfixing

## Failure-stage taxonomy

Classify the first failed invariant before editing:

| Stage | Typical symptoms | Primary owner/evidence |
|---|---|---|
| Admission | operation rejected, duplicate active run | activity coordinator/run start store |
| UI observation | missing/wrong surface, loading/mismatch | context registry/publication/navigation ID |
| Turn capture | mixed revisions, stale attachment | capture ID/digest/publication revision |
| Authority | unexpected denial or widening | authority resolver/policy fingerprint |
| Workspace scope | wrong project/path/tool denial | scope identity/service bundle |
| Capability composition | missing/duplicate/weaker tool | descriptor manifest/access diagnostics |
| Provider dispatch | lane timeout, wrong model/credential | provider query/runtime handle |
| MAF stream/session | lost update, empty response, resume failure | adapter state/update sequence |
| Tool invocation | sanitized error, receipt mismatch | tool trace/receipt/target path |
| Approval | wrong proposal, duplicate resume | run revision/pending-set hash/decision IDs |
| Structured output/finalizer | validation/sequence/repair mismatch | contract hash/finalizer trace |
| Persistence | result completed but session/run stale | atomic mutation/revision/log |
| Process | recovery/gate/branch/artifact failure | Processes policy and completion receipts |
| Workflow LLM | schema/usage/parity mismatch | lightweight request/result and workflow projection |
| UI refresh | run succeeded but visible projection stale | completion notification source/scope |

## Required correlation set

Record, when applicable:

- activity operation ID;
- execution run ID and revision;
- chat session ID;
- context capture ID/version/epoch/digest;
- authority ID/policy fingerprint;
- database profile ID/generation;
- workspace scope identity and service-bundle version;
- runtime port and adapter/schema version;
- provider profile/model/transport;
- tool call/proposal IDs;
- process run/step and workflow run/node IDs.

Do not log raw prompts, opaque attachments, secrets, physical paths beyond approved relative aliases, or arbitrary tool arguments.

## Bugfix loop

1. Reproduce deterministically with fixed IDs/time/fake provider where possible.
2. Capture the first invariant violation and correlation set.
3. Identify the canonical owner; distinguish symptom layer from cause layer.
4. Add a failing regression test at the owner boundary.
5. Compare before-state fixture, tool manifest, context/authority/scope fingerprints, and persisted state.
6. Implement the smallest cohesive fix.
7. Run focused unit/negative/fault tests.
8. Run architecture/source/dependency guards.
9. Run the current checkpoint matrix.
10. Update bug record, risk register, proof manifest, and session handoff.

## Prohibited bugfixes

- re-reading the current UI to make a continuation work;
- granting authority because a context/payload contains a project ID/path;
- resolving a missing dependency through `IServiceProvider` in runtime behavior;
- restoring a product reference in MAF;
- bypassing process completion gates;
- accepting stale artifacts or approvals by heuristic;
- silently resetting incompatible runtime state;
- reintroducing first-wins tool deduplication;
- calling the full agent runtime from the lightweight LLM path;
- swallowing cleanup/persistence failure without preserving the primary error.

## Regression exit criteria

A bug is closed only when the owner test fails before and passes after the fix, the original scenario passes, adjacent negative/fault tests pass, and no architecture guard regresses.
