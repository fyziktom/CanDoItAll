# Observability and regression plan

## Bounded telemetry fields

- operation/run/session identifiers;
- context capture/version/epoch/digest;
- authority ID/policy fingerprint;
- profile generation;
- workspace scope identity and bundle version;
- runtime port/adapter/state schema;
- provider/model/transport;
- tool/proposal IDs and sequence;
- process/workflow correlation IDs;
- failure stage/code;
- lightweight invocation ID, streaming sequence, finish disposition, and usage-owner marker;
- cutover selector/version and selected production path.

## Prohibited fields

- raw prompt/transcript unless an existing reviewed secure sink explicitly owns it;
- opaque context attachment payloads;
- secrets/credentials;
- arbitrary exception or tool argument text;
- physical external paths beyond reviewed aliases;
- serialized runtime-state payload.

## Regression layers

1. Source/dependency architecture guards.
2. Contract serialization and compatibility fixtures.
3. Direct unit tests for extracted owners.
4. Composition/lifetime smoke tests.
5. Fault-injection tests.
6. Integration tests for persistence/approvals/process/workflow/API.
7. Component tests for floating context and Gantt.
8. Manual rebuilt application acceptance.

## Golden comparisons

Capture and compare where deterministic:

- tool descriptor/approval manifest;
- context observation/transition metadata;
- runtime request/result projections;
- provider usage mapping;
- process completion receipts/gate order;
- public API JSON fields;
- provider driver invocation count and terminal usage source for lightweight calls;
- runtime handle/pool acquisition and disposal count in deterministic lifecycle tests.

Do not golden-test raw provider prose.


## Failure-stage taxonomy

Use one primary stage for every defect and attach downstream symptoms separately:

```text
admission
context-capture
authority-resolution
workspace-scope-construction
capability-composition
provider-dispatch
provider-stream
runtime-session
tool-policy-or-execution
approval-persistence-or-resume
output-or-finalizer-validation
execution-persistence
process-policy-or-completion
workflow-projection
lightweight-llm-mapping
ui-refresh-or-projection
cleanup-or-disposal
```

A fix belongs to the first stage whose invariant was violated, not necessarily the stage where the user-visible exception appeared. Persist the stage, stable failure code, operation/run IDs, and sanitized evidence in the bugfix record.
