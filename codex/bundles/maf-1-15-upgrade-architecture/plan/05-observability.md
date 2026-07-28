# Observability Plan

## Principles

- Emit structured fields, not raw prompts or unrestricted tool arguments.
- Redact paths, secrets, tokens, and personal data according to existing policy.
- Correlate framework, application session, execution run, process run, and approval ID.
- Distinguish compatibility outcomes from provider failures.

## Recommended Dimensions

```text
maf.release_train
maf.stable_package_version
maf.preview_package_version
maf.resolved_meai_version
agent.id
agent.runtime_kind
provider.kind
provider.transport
history.mode
session.source_framework_version
session.state_schema_version
session.restore_result
session.serialization_result
session.contains_provider_conversation_id
workflow.kind
workflow.response_projection
workflow.terminal_output_found
handoff.count
approval.source_framework_version
approval.binding_result
approval.compatibility_path
approval.request_count
approval.decision_count
approval.replay_rejected
approval.fingerprint_result
tool.policy_result
tool.requires_approval
workspace.scope_kind
a2a.operation
```

## Events

### Package/startup

- resolved package release train;
- direct/transitive mismatch;
- active migration feature flags;
- warning suppression inventory version.

### Session

- create;
- native restore;
- transcript fallback;
- provider-managed restore;
- deserialize failure category;
- serialize success/timeout/failure;
- attachment scrub success/failure;
- source/target framework version.

### Approval

- request surfaced;
- application record persisted;
- MAF state serialized;
- decision received;
- binding matched;
- response rebound;
- unknown/replay/cross-session rejected;
- legacy reissue;
- compatibility bridge used;
- exact tool invocation completed/failed.

Never log unrestricted arguments. Log a fingerprint and approved display summary.

### Workflow/handoff

- workflow started/completed;
- intermediate event count;
- terminal output found;
- projection strategy;
- handoff count/depth rejection;
- finalizer mode and outcome;
- response/history mismatch detected by diagnostics.

### File tools

- tool classification;
- scope decision;
- external target alias decision;
- approval policy;
- path redaction category;
- mutation result;
- denied traversal/reparse escape.

### A2A

- discovery/card;
- message/stream;
- session created/restored;
- cancellation;
- auth and redacted error category.

## Counters and Alerts

Alert on:

- any bound approval executing a different fingerprint;
- unknown approval execution count > 0;
- duplicate mutation execution;
- cross-session approval match;
- session restore failure rate above baseline;
- handoff terminal output missing where required;
- response/history semantic mismatch;
- workspace escape attempt succeeding;
- mixed 1.13/1.15 MAF assemblies;
- legacy bridge usage after migration deadline.

Track trends:

- finalizer repair rate before/after workflow fix;
- average approval pending count;
- legacy reissue backlog;
- serialization timeout rate;
- A2A failures;
- tool-policy denials;
- activity-to-first-update latency.

## Proof Artifacts

Store redacted logs under:

```text
.codex/bundles/maf-1-15-upgrade-architecture/proof/<subbundle>/telemetry/
```

Include field names and representative values, but no secrets or raw sensitive tool arguments.
