# MAF 1.22 upgrade and retained state

The adapter targets Microsoft.Agents.AI 1.22.0 and A2A preview 1.22.0-preview.260918.1. Shared dependency floors are Microsoft.Extensions.AI 10.10.0, relevant Microsoft.Extensions packages 10.0.12, OpenAI 2.13.0 and OpenTelemetry.Api 1.18.0. The resolved A2A SDK is 1.0.0-preview2. Existing provider-neutral project boundaries and signing settings remain in force.

## Runtime behavior

The approval cache stores immutable snapshots and requires durable pending approvals. Reusing an approval identifier with changed arguments, tool identity, call identity or MCP server fails before continuation. An unchanged batch can be rehydrated after cache loss. The SDK still owns native approval binding; application records do not synthesize native pending authority. Durable admission independently verifies source, policy, batch and effect identities.

Provider failures carry a nullable HTTP status through the runtime exception contract. Processes classify cancellation and permanent provider/configuration/permission failures before conservative transient text. Failed or canceled agent results are handled before finalizer JSON validation, preserving their execution ID and a safe diagnostic. Classification does not prove retry safety: the existing durable no-effects attestation, reconciliation requirements, retry budget and recovery ownership remain mandatory. Terminal failure logs use the persisted completion timestamp so that the canonical failure event remains inside its execution interval. A transient HTTP request can recover within the existing native request budget before a local tool proposal; exhausting that budget does not grant whole-assignment replay from an advisory attestation.

A2A endpoint construction releases both current and previously acquired resources on failure. Ownership transfers only on successful construction. Remote task responses must have `Completed` state before they can become successful tool results; working, input-required, canceled, rejected and failed tasks cannot be reported as empty successful completions. Such tasks require remote reconciliation before retrying.

Agent assignment repair only handles executor kinds that permit agent resolution. Workflow rework also preserves its sealed input instead of appending agent instructions. Resuming a workflow or person assignment preserves its original executor and typed workflow binding; a cancelled workflow cannot be replaced by an agent that reports an unrelated success.

Live process details expose an explicit **Cancel run** action for active runs and runs needing attention. The action uses the existing persisted cancellation and descendant-cascade service. Closing or navigating away from the view does not cancel the submitted command. A confirmed cancellation remains visible if refreshing the projection fails; an unconfirmed command outcome requires a fresh read before another action. Floating agent **Stop chat** still closes its handle and does not imply durable execution cancellation.

Agent conversations awaiting durable tool approval expose **Cancel pending run**. The command checks the original workspace authority and atomically rejects undispatched proposals. It preserves messages, checkpoints, journal segments and prior receipts, and cannot overwrite a continuation that already started. Process-owned and background work use their existing owner cancellation paths. Duplicate cancellation does not create another effect or log entry; leaving the view does not cancel the submitted command.

Missing, corrupt or mismatched approval checkpoint payloads are reported before applying consent. The UI preserves the pending state and explains how to restore the original checkpoint or cancel the pending run before reviewing fresh proposals in a new thread. Cancellation does not restore native approval state. Unknown tool outcomes require effect reconciliation before replacement work; a typed tool-policy denial remains a permissions failure rather than a transient provider retry.

## Retained-state decisions

| State family | 1.22 behavior |
|---|---|
| Genuine 1.20 ordinary local/framework or service-managed native session | Native deserialization is supported by the retained fixture tests, subject to the existing provider, history and fingerprint checks. |
| Genuine 1.20 native pending approval without an application admission journal | Native binding restores; approved work executes once, rejected work never executes, and repeated responses do not duplicate it. All current application authority checks still apply. |
| Genuine 1.20 settled native history | Remains readable and does not authorize another execution. |
| Application admission journal written with the old AI/OpenAI package fingerprint | Fails closed with `tool-admission.unsupported-protocol`. This includes pending, approved but unfinished, and settled retained journal checkpoints. No implicit SDK model migration is registered. |
| Pending/admitted work with unknown, absent or unverified native writer version | Fails closed and requires reconciliation. Verified native writer versions for the current 1.22 adapter are 1.20.0 and 1.22.0; provider/model/history/schema/policy checks remain independent. |
| Ordinary legacy unversioned conversation without pending/admitted work | Existing legacy envelope handling and canonical replay policy remain available. This is not permission to restore protected work. |
| Genuine 1.20 workflow external-input checkpoint | Covered separately through the workflow adapter using the original topology, version, payload hash, private continuation and authorization snapshot. It is independent of the agent tool-journal codec. |

The maintained fixtures are in [Maf120](../../tests/Support/Fixtures/Maf120/README.md). Their hashes and original writer versions are preserved. Tests distinguish native compatibility from the stronger application admission protocol. Do not alter stored version labels, clear an admission journal, or copy display history into private SDK approval state to make a continuation run.

## Deployment and reconciliation

Before deployment, drain dispatch or deliberately pause runs. Back up application databases, workspace journals, native checkpoints, receipts and artifacts at one consistent boundary. Verify the backup in a disposable PostgreSQL 18 environment. This upgrade does not upgrade the database engine or delete retained state.

Identify incompatible work by run/session identity and the safe compatibility or protocol diagnostic. Preserve history and artifacts for review. For an old admitted run, use the original compatible runtime against a consistent isolated restore to inspect its proposals and effect receipts, or implement and prove an explicit migration. Reconcile unknown effects by durable operation identity before deciding whether replacement work is safe. Fresh approval must come from the authorized owner; it does not erase a previously attempted effect.

A binary downgrade after 1.22 writes state is not a supported rollback by itself. Restore the matching databases, journals/checkpoints, receipts and artifacts together from the verified boundary. External effects survive a local rollback and still require reconciliation. No deployment or retained-environment rollback is performed by the upgrade tests.

## Migration applicability

| Release surface | Local decision and source |
|---|---|
| M01 package graph | Central MAF properties and root Extensions properties updated; direct OpenAI and OTel pins reconciled in MAF/Core. |
| M02 approval binding | Existing native agent/session binding retained; cache correction and native/journal restart tests protect authority and effect counts. |
| M03 per-run tools | Application composition remains scoped. Overlapping native sessions use distinct same-named schemas and preserve authorization middleware, receipts and finalizers. No global concurrent mutation option is enabled. |
| M04 workflows | `MafWorkflowCompiler` uses imperative `WorkflowBuilder`. Native checkpoint, terminal output, failure, cancellation and handoff owner tests remain required. No declarative workflow package is consumed. |
| M05 upstream session stores | No `AgentSessionStore` or delegating-store consumer. The application uses its own versioned runtime envelope and journal, not upstream store keys. |
| M06 declarative MCP sessions | No declarative MCP/custom HTTP-provider consumer. `McpCapabilityBuilder` uses the independently owned MCP client layer. |
| M07 Foundry identity | No Foundry hosted-agent package or delegated-identity consumer. No new provider is introduced. |
| M08 A2A run modes | Hosting uses `AddA2AServer` without old run-mode names or a persisted upstream run-mode enum. The remote client is independently protected by lifetime and terminal-status tests. |
| M09 upstream file-line API | No upstream `AgentFileStore`, `FileMemoryProvider` or `file_access_read_lines` consumer. Application/FileTools line contracts retain their own portability tests. |
| M10 LocalCodeAct | Not registered or referenced. Existing shell/FileTools drivers retain their own host and approval boundaries. |
| M11 upstream MCP skills archives | No upstream MCP-skills archive importer is registered. Independent application importers retain their own supported formats. |
| M12 security/diagnostics | Application skill paths, MCP clients, provider-history accounting and telemetry remain owned by their current modules; their existing tests run in the required owner/platform gates. No unrelated hosted-storage integration is added. |

## Validation status

The 2026-09-25 recovery checkpoint is ready for GitHub CI. The current operator instruction leaves the next full-suite run to CI; local verification uses focused tests and the mandatory portability gate. The recovered Windows and Linux Stable results each contain 15,256 passes, zero failures and zero skips. Fresh verification passed the two regressions from the supplied CI logs, 448 MAF/process unit cases and 20 cancellation component cases. See the [validation handoff](maf-1.22-validation.md) for discovery counts, source correspondence and reproduction commands.

CI readiness does not close release acceptance. The full Playwright suites and remaining UI/live-provider journeys in the preparation bundle are still outstanding. The ordinary CI workflow builds Playwright but does not execute those full browser suites. Preserve that distinction, the retained-state restrictions above and historical evidence provenance; do not label the upgrade fully accepted until the remaining gates pass.
