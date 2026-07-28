# MAF 1.15 Migration Execution Report

## Status

- Overall: `Implemented and validated for the requested development scope`
- Current subbundle: `SB08 closure`
- A1 baseline/fixture gate: `Pass`
- A2 approval security/state gate: `Pass`
- A3 runtime semantics gate: `Pass`
- Development compatibility validation: `Pass`
- A4 production general-rollout gate: `Not asserted`

The source upgrade is complete. The production rollout gate remains deliberately
separate because the user requested a local rebuilt 5032 instance, excluded
process E2E, and did not authorize a production canary or rollback exercise.

## Repository State

| Field | Value |
|---|---|
| Repository | `fyziktom/CanDoItAll` |
| Branch | `agents-loading-refactor` |
| Prepared bundle head | `59f558bc866d39d438b53f5f743dd5e87c2a6253` |
| Execution head | `797d7ce11205d630756ec9335b1b84295257a315` plus the intended working-tree migration |
| .NET SDK | `10.0.204` |
| OS | Windows |
| Stable MAF resolved version | `1.15.0` |
| A2A/hosting resolved version | `1.15.0-preview.260722.1` |
| Microsoft.Extensions.AI | Existing `10.8.0` line preserved |

## Subbundle Progress

| Subbundle | Status | Proof/result |
|---|---|---|
| SB01 | Complete | `proof/SB01/` baseline, package graph, discovery, fixtures, warning inventory, and rollback boundary |
| SB02 | Complete | Shared stable/preview properties; package alignment validator passed |
| SB03 | Complete | `proof/SB03/final-validation.md`; final focused unit slice 71/71 |
| SB04 | Complete | Direct and streaming handoff slice 6/6 |
| SB05 | Complete | Native 1.15 session round-trip/scrub/restore tests |
| SB06 | Complete for compatibility scope | Custom workspace tools remain canonical; no Harness replacement or SDK leakage |
| SB07 | Complete for compatibility scope | Preview packages compile; A2A surface parity is inactive before/after; 9/9 metadata/card/remote-tool tests |
| SB08 | Complete for development compatibility scope with explicit open gates | `proof/SB08/final-validation.md`; rebuild, live UI, health, and residual-risk record |

## Requirement Closure

| Requirement | Status | Evidence or explicit exception |
|---|---|---|
| R01 | Pass | `proof/SB01/` contains baseline graph, discovery, warnings, fixture manifests, and rollback boundary. |
| R02 | Pass | Alignment script observed one stable `1.15.0` and preview `1.15.0-preview.260722.1` train; no 1.13 package remained. |
| R03 | Pass | `src/MAF/MicrosoftAgentFramework.Packages.props` is imported by the three direct MAF projects; CPM was not introduced. |
| R04 | Pass | Invocation-scoped runtime architecture was retained; architecture gate and focused runtime tests found no new mutable sharing. |
| R05 | Pass | Approval binding is explicitly enabled at the common options seam; native function and hosted-MCP restart/binding tests passed. |
| R06 | Pass | Native 1.15 session state is required; incompatible legacy state is drain/reissue only; no private-JSON reconstruction bridge exists. |
| R07 | Pass | Complete server-held snapshot remains the authority; stable request/call IDs and exact-once continuation are covered. |
| R08 | Pass | Random approval-ID fallback was removed; missing stable identity fails predictably. |
| R09 | Pass | Mixed-tool parity is explicit through `DisableApprovalNotRequiredFunctionBypassing = true`. |
| R10 | Pass | Direct and streaming handoff terminal-output comparison passed 6/6. |
| R11 | Pass | Response assembly/order/history characterization is covered by focused runtime and handoff tests. |
| R12 | Pass with typed legacy outcome | Native 1.15 round-trip and attachment scrub pass; incompatible 1.13 approval state is not reconstructed. |
| R13 | Pass for unchanged boundary | CanDoItAll workspace/file tools remain authoritative; the live denial probe proved no mutation. |
| R14 | Pass | Discovery classified Harness/FileAccess as non-canonical and did not adopt it. |
| R15 | Compatibility parity, not inbound hosting | The product maps no inbound A2A server before or after. Preview packages compile and 9/9 card/metadata/remote-tool tests pass. |
| R16 | Pass | Optional MAF features were inventoried but not adopted. Existing compaction remains explicit. |
| R17 | Pass with inherited warning | No new blanket suppression was added. The inherited `System.Security.Cryptography.Xml` warnings remain visible. |
| R18 | Pass | Required-finalizer governance is retained; non-required work now exits before unnecessary projection work. |
| R19 | Pass | Serialization timeout, caller cancellation, malformed/missing state, and persistence failure are explicit and tested. |
| R20 | Not executed for production | Development canary is healthy. Production canary/rollback execution was outside the authorized local scope. |
| R21 | Pass | Architecture gate passed; no new cycle, project reference, partial class, SDK leakage, or non-English source comment. |
| R22 | Pass for development closure | This report links all requirements and owns the inbound A2A and production-rollout exceptions. |

## Package Graph

### Before

```text
Microsoft.Agents.AI                  1.13.0
Microsoft.Agents.AI.OpenAI           1.13.0
Microsoft.Agents.AI.Workflows        1.13.0
Microsoft.Agents.AI.A2A              1.13.0-preview.260703.1
Microsoft.Agents.AI.Hosting.A2A      1.13.0-preview.260703.1
```

### After

```text
Microsoft.Agents.AI                  1.15.0
Microsoft.Agents.AI.Abstractions     1.15.0
Microsoft.Agents.AI.OpenAI           1.15.0
Microsoft.Agents.AI.Workflows        1.15.0
Microsoft.Agents.AI.A2A              1.15.0-preview.260722.1
Microsoft.Agents.AI.Hosting          1.15.0-preview.260722.1
Microsoft.Agents.AI.Hosting.A2A      1.15.0-preview.260722.1
```

No adjacent package downgrade was needed.

## Implementation Results

### Provider and Approval Pipeline

- One factory owns all `ChatClientAgentOptions`.
- Default MAF middleware remains enabled.
- Approval-response binding is explicitly enabled.
- 1.13 mixed-tool parity remains explicit.
- Approval-capable content requires a stable request ID or call ID.
- Native MAF session state is serialized, scrubbed, restored, and rebound.
- Serialization uses linked cancellation and a five-second timeout; actionable
  approval cannot escape without persisted state.

### Runtime and Workflows

- Non-required finalizer projection exits before accumulated snapshot/usage work.
- Direct handoff delegates to the complete native non-streaming response.
- Production streaming retains incremental depth enforcement and selects the
  exact terminal `WorkflowOutputEvent`.
- Approval activity now projects `WaitingOnTool / Approval` to
  `AwaitingApproval`, preserving the valid
  `PersistingResult -> AwaitingApproval -> Suspended` sequence.

### UI Context

- Closing a persisted Recruiting record restores editor state from the loaded
  workspace while retaining route, browser selection, and typed agent context.
- Unsaved-new close/reopen behavior remains unchanged.

## Validation Results

| Validation | Result |
|---|---|
| Bundle validator | Passed, 151 files |
| Package alignment | Passed |
| `git diff --check` | Passed; line-ending notices only |
| Final solution rebuild | Passed, exit 0, operation `op_441c0b94463648c68f25f0dccc985c5b` |
| Final focused MAF/approval/activity unit slice | 71/71 |
| Handoff integration | 6/6 |
| Entry-surface unit slice | 107/107 |
| Recruiting retained-context component slice | 2/2 |
| A2A metadata/card/remote-tool slice | 9/9 |
| Scheduler-to-workflow boundary integration | 1/1 |
| Relevant component slice | 69/70, then the single timeout passed 1/1 |
| Selected Playwright slice | 2/3, then the single startup timeout passed 1/1 |

No process E2E suite was run.

## Live UI Results

| Surface | Result |
|---|---|
| Agent shell | Real OpenAI chat completed `MAF-1.15-LIVE-OK` and a second message in the same thread. |
| Approval | Exact request survived persistence, reached actionable approval, restored after denial, and did not create the target file. |
| Workflow editor | Real provider run completed with 33 events and terminal `WorkflowOutputEvent`. |
| Project Structure | Contextual chat completed `PROJECT-CONTEXT-OK`; workflow launch/inspect Playwright coverage passed on rerun. |
| Scheduler | Contextual chat completed `SCHEDULER-CONTEXT-OK`; scheduler-to-workflow integration passed. |
| Process step | Focused executor/hardening tests passed; process E2E deliberately excluded. |
| Recruiting | Retained Viktor Petrov context; HR Staffing Manager completed and answered exactly `Viktor Petrov`. |

## Hosting and Runtime

- `http://localhost:5032/health`: `200`
- `http://localhost:5032/.well-known/agent-card.json`: `404`, expected
  because no inbound A2A route exists in the product baseline
- Managed session: `app_b996d1823dfa4a279288dee34e196a85`
- Revision: `candoitall-web-5032:1:g0`
- State: `Healthy`
- Runtime PID: `10104`; watcher PID: `51576`
- Launch override:
  `--Processes:RuntimeDispatchQueue:EnableRecovery=false`

The override isolates an unrelated stale process-recovery EF query so managed
health reflects the MAF/UI runtime. It does not disable Scheduler, workflows, or
agent execution. The instance is left running for user testing.

## Warning Review

`System.Security.Cryptography.Xml` `10.0.7` continues to emit inherited
high-severity `NU1903` advisories:

- `GHSA-23rf-6693-g89p`
- `GHSA-8q5v-6pqq-x66h`
- `GHSA-cvvh-rhrc-wg4q`
- `GHSA-g8r8-53c2-pm3f`
- `GHSA-mmjf-rqrv-855v`

They were not introduced or suppressed by this MAF upgrade.

## Final Decision

- Development implementation and validation: `GO`
- Dedicated hosted-MCP approval restart fixture: `PASS`
- Production general rollout: `NOT ASSERTED`
- Legacy reconstruction bridge absent: `Yes`
- Inbound A2A feature adoption: `No; preserved inactive parity`
- Process E2E: `Not run by explicit request`
- Date: `2026-07-28`
