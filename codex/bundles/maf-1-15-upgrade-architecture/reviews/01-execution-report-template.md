# MAF 1.15 Migration Execution Report

## Status

- Overall: `Not started`
- Current subbundle: `SB01`
- A1: `Pending`
- A2: `Pending`
- A3: `Pending`
- A4: `Pending`

## Repository State

| Field | Value |
|---|---|
| Repository | |
| Branch | |
| Baseline head | |
| Final head | |
| Working tree | |
| .NET SDK | |
| OS | |
| Stable MAF resolved version | |
| A2A resolved version | |
| MEAI resolved version | |

## Subbundle Progress

| Subbundle | Status | Commit(s) | Proof root | Gate/result | Notes |
|---|---|---|---|---|---|
| SB01 | Not started | | | | |
| SB02 | Not started | | | | |
| SB03 | Not started | | | | |
| SB04 | Not started | | | | |
| SB05 | Not started | | | | |
| SB06 | Not started | | | | |
| SB07 | Not started | | | | |
| SB08 | Not started | | | | |

## Requirement Closure

| Requirement | Status | Source changes | Tests | Proof | Exceptions |
|---|---|---|---|---|---|
| R01 | | | | | |
| R02 | | | | | |
| R03 | | | | | |
| R04 | | | | | |
| R05 | | | | | |
| R06 | | | | | |
| R07 | | | | | |
| R08 | | | | | |
| R09 | | | | | |
| R10 | | | | | |
| R11 | | | | | |
| R12 | | | | | |
| R13 | | | | | |
| R14 | | | | | |
| R15 | | | | | |
| R16 | | | | | |
| R17 | | | | | |
| R18 | | | | | |
| R19 | | | | | |
| R20 | | | | | |
| R21 | | | | | |
| R22 | | | | | |

## Package Graph

### Before

```text
Attach path or summary.
```

### After

```text
Attach path or summary.
```

### Adjacent Dependency Changes

| Package | Before | After | Reason | Compatibility proof |
|---|---:|---:|---|---|

## Persisted State Compatibility

| Fixture | Source version | Target version | Result | Path used | Proof |
|---|---:|---:|---|---|---|
| Empty local session | 1.13 | 1.15 | | | |
| Local history | 1.13 | 1.15 | | | |
| Provider conversation | 1.13 | 1.15 | | | |
| Function approval | 1.13 | 1.15 | | | |
| MCP approval | 1.13 | 1.15 | | | |
| Attachment scrub | 1.13/1.15 | 1.15 | | | |
| Governed step | 1.13 | 1.15 | | | |
| Background response | 1.13 | 1.15 | | | |
| Workflow checkpoint | 1.13 | 1.15 | | | |
| Rollback fixture | 1.15 | 1.13 | | | |

## Approval Security Results

| Scenario | Expected | Actual | Tool invoked? | Proof |
|---|---|---|---|---|
| Correct approval | exact original call once | | | |
| Modified tool name | rebound/rejected | | | |
| Modified arguments | rebound/rejected | | | |
| Unknown ID | reject | | | |
| Cross-session | reject | | | |
| Duplicate | once | | | |
| Replay | reject | | | |
| Denial | no invoke | | | |
| Legacy direct response-only | reject native continuation; drain/reissue | | | |
| Legacy reconstruction attempt | reject | | | |
| MCP | exact original call | | | |
| Scrubbed restart | exact original call | | | |

## Workflow and Merge Results

| Path | Terminal output | Activity retained | Tool/result order | History match | Proof |
|---|---|---|---|---|---|
| Inner RunAsync | | | | | |
| Inner RunStreamingAsync | | | | | |
| Depth guard RunAsync | | | | | |
| Depth guard streaming | | | | | |
| Full MafAgentRuntime | | | | | |
| Persisted workflow history | | | | | |

## File/Capability Results

| Area | Baseline | Final | Result | Proof |
|---|---|---|---|---|
| Tool inventory | | | | |
| Traversal | blocked | | | |
| Junction/reparse escape | blocked | | | |
| External alias | policy | | | |
| Read-only target | blocked mutation | | | |
| Script policy | enforced | | | |
| Unsupported provider approval | fail closed/filter | | | |
| Concurrent workspace isolation | isolated | | | |

## A2A Results

| Scenario | Result | Proof |
|---|---|---|
| Host startup | | |
| Agent card | | |
| Message | | |
| Stream | | |
| Session | | |
| Approval if supported | | |
| Cancellation | | |
| Auth/error redaction | | |

## Workaround Closure

| Workaround ID | Final decision | Source change | Proof | Follow-up |
|---|---|---|---|---|

## Warning Review

| Warning | Location | Decision | Suppression scope | Rationale |
|---|---|---|---|---|

## Canary and Rollback

| Rehearsal | Result | State snapshot | Telemetry | Proof |
|---|---|---|---|---|
| Staging state copy | | | | |
| No-pending canary | | | | |
| Legacy reissue | | | | |
| No reconstruction bridge | | | | |
| Rollback | | | | |

## Commands Executed

```text
Record exact commands and exit codes.
```

## Inherited Failures

| Failure | Baseline evidence | Migration impact | Owner/exception |
|---|---|---|---|

## Deferred Optional Work

| Feature | Decision | Rationale | Follow-up bundle/issue |
|---|---|---|---|

## Final Decision

- A4: `Pending`
- Production mutation traffic safe: `Not evaluated`
- Legacy reconstruction bridge absent: `Not evaluated`
- Legacy approval backlog: `Unknown`
- Rollback proven: `No`
- Reviewer:
- Date:
