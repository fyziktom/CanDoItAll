# SB06 — File Tools and Capability Security Regression

## Status

- `Complete for compatibility scope`

## Objective

Prove that the MAF upgrade does not change CanDoItAll's custom file, command, artifact, capability, authorization, approval, or runtime-isolation boundaries.

## Success Criteria

- Every Harness/FileAccess discovery match is resolved.
- Representative before/after tool inventories are equivalent or intentionally documented.
- No unexpected MAF file tools or duplicate tool names appear.
- Workspace root, scope, external aliases, read-only targets, process operations, and script policies remain enforced.
- Mutation tools retain the correct approval behavior for supported and unsupported providers.
- Concurrent executions share no mutable file/tool/authorization state.
- DI fallback and registered services use the correct workspace scope.

## Covered Requirements

- R04, R05, R13, R14, R21, R22

## Prerequisites

- A3 GO;
- baseline tool inventory;
- custom workspace/capability paths located;
- approval security complete.

## Exact Source References

- `AgentFrameworkServiceCollectionExtensions.cs`
- `MafRuntimeDependencyResolver.cs`
- `MafRuntimeAgentFactory.cs`
- capability composer/resolver files
- workspace file/path/command/artifact services
- FileTools integration projects
- tool policy and script inspection
- provider capability filtering
- relevant tests

## Deliverables

- Harness/FileAccess resolution report;
- before/after tool inventories;
- path/scope security tests;
- mutation/approval/provider tests;
- concurrency/isolation tests;
- `proof/SB06/file-tools-security.md`.

## Implementation Steps

1. Re-run Harness/FileAccess discovery on final code.
2. Resolve every match and verify no accidental Harness provider.
3. Generate representative tool inventories.
4. Compare tool names, schemas, descriptions, wrappers, owners, and context providers.
5. Run path traversal/junction/symlink/root containment tests.
6. Run external alias and read-only tests.
7. Run process allowed-operation and product mutation tests.
8. Run governed script side-effect tests.
9. Run approval-supported/unsupported provider matrix.
10. Test explicitly authorized suppression paths.
11. Run concurrent distinct-workspace/provider/session executions.
12. Verify disposal and no blueprint/live-state contamination.
13. Review audit/telemetry redaction.

## Do Not Do

- do not replace custom tools with Harness file access;
- do not add duplicate file tool names;
- do not weaken root/scope checks;
- do not rely solely on MAF approval wrapper for application authorization;
- do not share mutable tool instances across runs;
- do not broaden external target aliases.

## Acceptance Checklist

- [x] all Harness matches resolved
- [x] no unexpected MAF file provider
- [x] tool inventory reviewed
- [x] path/reparse behavior remains on the existing canonical implementation
- [x] alias/read-only behavior remains on the existing canonical implementation
- [x] script/process policy remains unchanged
- [x] provider approval matrix remains behind the common options seam
- [x] concurrency isolation architecture passes
- [x] audit redaction behavior remains unchanged

## Proof Tier

- `Governed`

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

SB07 may close only after file/capability security has no unexplained delta.

## Reopen Triggers

- file/capability package changes;
- workspace scope model changes;
- new provider transport;
- new tool composition cache;
- Harness adoption proposal.

## Suggested Agent Prompt

```text
Implement SB06 only. Prove the custom CanDoItAll file/capability boundary is unchanged after MAF 1.15, resolve every Harness match, compare tool inventories, test path/scope/alias/script/approval security and concurrent isolation, and do not replace the custom tools.
```
