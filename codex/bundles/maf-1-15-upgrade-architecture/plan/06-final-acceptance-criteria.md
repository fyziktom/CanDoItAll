# Final Acceptance Criteria

Checked items are proven for the requested development compatibility scope.
Unchecked items are explicit production or feature-expansion gates. They do not
silently become passes.

## Package

- [x] shared stable version is `1.15.0`
- [x] shared preview version is `1.15.0-preview.260722.1`
- [x] no observed direct or transitive 1.13 MAF assembly
- [x] no NuGet downgrade
- [x] adjacent dependency changes are minimal and proven
- [x] A2A release train matches

## Approval and State

- [x] binding active through the common provider path
- [x] initial mixed-call parity behavior explicit
- [x] decision bound to the complete current server-held pending snapshot
- [x] stable persisted request and call IDs
- [x] no random ID fallback
- [x] exact request/tool/arguments authority
- [x] atomic session/snapshot persistence and at-most-once consumption
- [x] forged/substituted/replay/cross-session tests pass
- [x] native function-approval restart test passes
- [x] dedicated hosted-MCP approval restart fixture execution
- [x] legacy incompatible state has a drain/reissue policy
- [x] scrubbed session retains native approval binding
- [x] no private-JSON classifier or reconstructed compatibility bridge

## Workflow and Responses

- [x] explicit terminal output authoritative
- [x] intermediate activity retained
- [x] no duplicate workflow execution
- [x] handoff depth enforced on the production streaming path
- [x] tool-call/result adjacency
- [x] reasoning/text order
- [x] response and history contract
- [x] ordinary agents unchanged
- [x] finalizer governance preserved
- [x] usage not double-counted

## Sessions and Checkpoints

- [x] 1.13 fixture manifest matrix captured
- [x] native 1.15 round-trip
- [x] provider-conversation compatibility outcome documented
- [x] no transcript duplication in characterized paths
- [x] governed-step isolation retained
- [x] attachment bytes absent after scrub
- [x] arbitrary non-attachment state retained
- [x] timeout/cancellation/error diagnostics
- [x] native checkpoint result documented
- [x] rollback state boundary documented

## File and Capability Security

- [x] Harness/FileAccess matches classified
- [x] custom CanDoItAll tools remain canonical
- [x] no duplicate tool names introduced
- [x] path/traversal/reparse/root containment implementation unchanged
- [x] external aliases and read-only rules unchanged
- [x] process-operation scope unchanged
- [x] script side-effect policy unchanged
- [x] approval provider matrix remains behind the common options seam
- [x] concurrent run isolation architecture retained
- [x] audit redaction behavior unchanged

## Hosting and Optional Features

- [x] A2A compatibility parity documented: no inbound route before or after
- [x] A2A metadata/card/remote-tool tests pass 9/9 on the matching preview train
- [x] AG-UI status inventoried; not adopted
- [x] declarative workflow status inventoried; not adopted
- [x] Harness/FileMemory status inventoried; not adopted
- [x] ToolApprovalAgent status inventoried; not adopted
- [x] message-injection status inventoried; not adopted
- [x] existing compaction behavior retained
- [x] CodeAct/shell status inventoried; not adopted
- [x] Cosmos status inventoried; not adopted
- [x] Responses hosting status inventoried; not adopted
- [x] no optional unreviewed adoption
- [ ] new inbound A2A host/card/message/stream/session deployment

## Closure

- [x] workaround register closed for the compatibility implementation
- [x] no new warning suppression; inherited warnings recorded
- [x] focused deterministic migration and entry-surface tests
- [x] full solution rebuild
- [x] real-provider agent and workflow validation
- [x] approval restart/denial validation
- [x] process-step validation without process E2E
- [x] local 5032 development canary
- [ ] production canary and rollback rehearsal
- [x] runtime telemetry reviewed
- [x] requirement traceability reconciled
- [x] independent architecture review is GO for the code changes
- [x] development compatibility validation complete with owned exceptions
- [ ] A4 production general-rollout GO
