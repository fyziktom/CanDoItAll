# Rollout and Rollback Plan

## Pre-Deployment

- complete all deterministic gates;
- create a state-store backup;
- inventory active sessions and pending approvals;
- classify pending approvals by source framework version;
- stop or limit creation of new long-lived approvals during the migration window;
- record branch/commit, package graph, schema versions, and configuration flags;
- verify rollback artifacts and binaries are available;
- verify no secret is present in fixtures or logs.

## Feature Flags

Recommended flags:

```text
Maf115ApprovalNotRequiredBypassing
Maf115LegacyApprovalBridge
Maf115WorkflowTerminalProjection
Maf115SessionCompatibilityDiagnostics
```

Defaults for first deployment:

- approval response binding: always enabled, not a flag;
- approval-not-required bypass: disabled for parity;
- legacy bridge: disabled unless operationally required;
- terminal projection: enabled only after SB04 proof, otherwise deployment blocked;
- diagnostics: enabled.

## State Handling Before Cutover

### No pending approval

Safe canary candidate after fixture validation.

### Native 1.13 pending approval

Preferred:

- mark as `RequiresReissueAfterFrameworkUpgrade`;
- preserve display/audit record;
- do not auto-approve;
- re-run owning step under 1.15.

### Long-lived workflow checkpoint

Resume in staging from copied/sanitized state before production cutover.

### Provider-managed conversation

Preserve provider ID and avoid transcript duplication.

## Canary Sequence

1. deploy to isolated/staging state copy;
2. run package/build/A2A health checks;
3. run ordinary agent and file-tool read;
4. run approval request, restart, approve;
5. run handoff terminal-output fixture;
6. run governed process step;
7. enable a small production canary cohort with no legacy pending approvals;
8. monitor session deserialize, binding, merge, and tool-policy metrics;
9. migrate/reissue legacy approvals;
10. only after stability consider enabling 1.15 approval-not-required bypass for a canary.

## Rollback Triggers

Immediate rollback or traffic stop:

- approval executes a different tool/argument than displayed;
- unknown/replayed approval invokes a tool;
- cross-session state leakage;
- file path escapes workspace policy;
- handoff returns an intermediate response as machine output;
- widespread session deserialize failure;
- A2A session leakage or auth regression;
- duplicate mutation/tool execution;
- unresolved package train mismatch.

Investigate before rollback:

- increased warning/log volume;
- missing usage metadata;
- non-security message formatting differences;
- optional bypass feature issue when it can be disabled independently.

## Rollback Procedure

1. stop new mutation/approval traffic;
2. disable optional 1.15 behavior flags;
3. snapshot the 1.15-written state for analysis;
4. restore pre-deployment state backup for active approvals unless bidirectional compatibility was proven;
5. deploy known 1.13 binaries/package graph;
6. verify provider conversation and workflow checkpoint behavior;
7. reconcile runs created during the canary;
8. record exact failed fixture and reopen owning subbundle.

Do not feed 1.15-created pending approvals into 1.13 without explicit proof.

## Data Reconciliation

Track:

- approvals reissued;
- approvals expired;
- approvals bridged;
- approvals executed;
- duplicate/replay attempts rejected;
- sessions restored natively;
- sessions replayed from transcript;
- sessions rejected/incompatible;
- workflow checkpoints resumed;
- tool mutations during canary.

Every mutation must be traceable to one run, one approval request where required, and one application audit record.

## Completion

The rollout is complete only after:

- legacy pending approval count is zero;
- temporary bridge is disabled and scheduled for removal;
- canary and full cohort metrics are stable;
- rollback rehearsal evidence is attached;
- state backup retention policy is recorded;
- optional new behavior is either deliberately enabled with proof or remains explicitly deferred.
