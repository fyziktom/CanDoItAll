# Final Acceptance Criteria

## Package

- [ ] shared stable version is `1.15.0`
- [ ] shared preview version is `1.15.0-preview.260722.1`
- [ ] no direct or transitive 1.13 MAF assembly
- [ ] no NuGet downgrade
- [ ] adjacent dependency changes are minimal and proven
- [ ] A2A release train matches

## Approval and State

- [ ] binding active for every provider path
- [ ] parity mixed-call behavior explicit
- [ ] decisions addressed by approval ID
- [ ] no random ID fallback
- [ ] exact request/tool/arguments authority
- [ ] exact-once transactional consumption
- [ ] forged/substituted/replay/cross-session tests pass
- [ ] function and MCP restart tests pass
- [ ] legacy 1.13 state reissues or uses approved temporary bridge
- [ ] scrubbed session retains binding
- [ ] bridge disabled or removal plan accepted

## Workflow and Responses

- [ ] explicit terminal output authoritative
- [ ] intermediate activity retained
- [ ] no duplicate workflow execution
- [ ] handoff depth enforced
- [ ] tool-call/result adjacency
- [ ] reasoning/text order
- [ ] response and history contract
- [ ] ordinary agents unchanged
- [ ] finalizer governance preserved
- [ ] usage not double-counted

## Sessions and Checkpoints

- [ ] 1.13 fixture matrix complete
- [ ] native 1.15 round-trip
- [ ] provider conversation ID preserved
- [ ] no transcript duplication
- [ ] governed step isolation
- [ ] attachment bytes absent
- [ ] arbitrary state retained
- [ ] timeout/cancellation/error diagnostics
- [ ] native checkpoint result documented
- [ ] rollback result documented

## File and Capability Security

- [ ] all Harness matches classified
- [ ] custom tools remain canonical
- [ ] no duplicate tool names
- [ ] traversal/reparse/root containment
- [ ] external aliases and read-only rules
- [ ] process operation scope
- [ ] script side-effect policy
- [ ] approval provider matrix
- [ ] concurrent run isolation
- [ ] audit redaction

## Hosting and Optional Features

- [ ] A2A host/card/message/stream/session/security smoke
- [ ] AG-UI status
- [ ] declarative status
- [ ] Harness/FileMemory status
- [ ] ToolApprovalAgent status
- [ ] message injection status
- [ ] compaction status
- [ ] CodeAct/shell status
- [ ] Cosmos status
- [ ] Responses hosting status
- [ ] no optional unreviewed adoption

## Closure

- [ ] workaround register closed with proof
- [ ] warnings narrowed
- [ ] full deterministic tests
- [ ] full solution build
- [ ] real provider validation
- [ ] approval restart validation
- [ ] governed process validation
- [ ] canary rehearsal
- [ ] rollback rehearsal
- [ ] telemetry reviewed
- [ ] requirement traceability complete
- [ ] independent QA GO
- [ ] A4 GO
