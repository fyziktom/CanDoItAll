# Independent QA and Architecture Review Prompt

```text
Review the completed MAF 1.15 migration as an independent senior C# runtime/security architect.

Do not assume the implementation report is correct. Inspect source, package assets, tests, state fixtures, logs, and changed behavior.

Review dimensions:

1. Package integrity
- stable MAF packages resolve exactly to 1.15.0;
- A2A packages resolve exactly to the matching preview release train;
- no 1.13 transitive assembly remains;
- no unrelated dependency downgrade or hidden optional package adoption.

2. Runtime lifetime
- immutable preparation cache only;
- no live agent/session/tool/MCP/provider/approval/request-state pooling;
- all run-owned resources disposed exactly once;
- concurrent runs cannot exchange state.

3. Approval security
- binding is active on every provider path;
- custom chat-client stacks do not bypass binding;
- decisions are admitted only for the exact complete current server-held pending
  snapshot;
- stable persisted request and call IDs are preserved;
- server-held request is authoritative;
- missing/random IDs fail closed;
- substituted tool/arguments are rebound or rejected;
- unknown, replayed, duplicate, stale, and cross-session approvals execute nothing;
- legacy 1.13/incompatible state is drained or reissued, never reconstructed;
- no private-JSON classifier, per-ID migration state, approval fingerprint
  layer, or compatibility bridge was introduced;
- scrubbed serialized state retains binding data;
- function and MCP shapes are tested;
- a changed or partial pending snapshot cannot receive an old decision.

4. Mixed-tool behavior
- parity phase explicitly disables new default bypass;
- optional later enablement has its own tests and feature gate;
- CanDoItAll mutation classification remains authoritative.

5. Workflow and response semantics
- production streaming runtime returns explicit terminal workflow output;
- intermediate participant responses are activity, not machine output;
- no duplicate workflow execution;
- max handoff depth remains enforced;
- tool-call/result adjacency and reasoning/text order are correct;
- response and persisted history contracts are both tested;
- no timestamp sorting or fragile reflection into MAF internals.

6. Sessions and checkpoints
- 1.13 fixtures captured before package edits;
- native 1.15 round-trip;
- provider conversation IDs preserved without transcript duplication;
- governed step isolation preserved;
- attachment bytes removed, arbitrary state retained;
- persistence failures are categorized;
- workflow checkpoint claim is backed by a native fixture;
- rollback direction is tested or state restore is mandatory.

7. File/capability security
- no accidental Harness file provider;
- custom workspace/file/command/artifact tools remain canonical;
- paths, traversal, reparse points, aliases, read-only targets, scripts, process operations, and provider approval support are tested;
- tool inventory has no unexplained delta or duplicate names.

8. A2A and optional scope
- A2A card/message/stream/session/security smoke evidence exists;
- AG-UI/Harness/declarative/FileMemory/compaction/message-injection/ToolApprovalAgent/Responses-hosting decisions are explicit;
- no optional redesign is hidden in compatibility changes.

9. Cleanup and proof
- every removed workaround has failing-first evidence;
- finalizer and application policy were not weakened;
- warning suppressions are narrow and justified;
- full and real-provider validations exist;
- canary/rollback rehearsal exists;
- execution report maps every requirement.

Produce:
- GO / NO-GO per A1-A4;
- findings ordered P0-P3;
- exact source/test/proof references;
- missing adversarial tests;
- rollback blockers;
- explicit statement whether production mutation traffic is safe.
```
