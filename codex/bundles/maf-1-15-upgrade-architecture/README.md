# Microsoft Agent Framework 1.15 Upgrade Architecture

This bundle is an execution-ready coordination package for upgrading CanDoItAll from Microsoft Agent Framework 1.13 to the 1.15 release train.

## Profile

- `initiative`
- `runtime-migration`
- `security-sensitive`
- `cross-version-state-migration`

## Repository Snapshot

- Repository: `fyziktom/CanDoItAll`
- Branch: `agents-loading-refactor`
- Inspected head: `59f558bc866d39d438b53f5f743dd5e87c2a6253`
- Bundle preparation date: `2026-07-27`
- Preparation status: `Prepared`
- Implementation status: `Not started`

The repository evidence in this bundle is pinned to the inspected head. Codex must compare the actual working-tree head to this SHA before editing and record any drift.

## Mission

Upgrade the stable MAF packages to `1.15.0` and the A2A packages to the matching `1.15.0-preview.260722.1` release train while preserving CanDoItAll's runtime isolation, workspace security, approval governance, session persistence, handoff semantics, A2A behavior, and existing agent-loading refactor invariants.

## Outcome Contract

The migration is complete only when all of the following are true:

- stable and preview MAF packages are aligned to the same 1.15 release train;
- current 1.13 runtime behavior is characterized before package changes;
- pending approvals created under 1.13 cannot bypass, confuse, or silently fail the 1.15 approval-response binding;
- handoff streaming and non-streaming paths return the intended terminal output without losing intermediate activity reporting;
- tool-call and tool-result order remains valid in returned responses and persisted history;
- serialized chat sessions and any native MAF workflow checkpoints are tested across the version boundary;
- CanDoItAll's custom workspace/file tools retain the same scope, authorization, approval, and path-safety behavior;
- A2A hosting restores, builds, and passes a real smoke test;
- no live agent, session, mutable tool, MCP client, approval state, or provider conversation is pooled across executions;
- obsolete workarounds are removed only after a failing-first characterization proves that MAF 1.15 supersedes them;
- rollout, rollback, telemetry, and persisted-state handling are documented and proven.

## Hard Constraints

- Do not replace CanDoItAll workspace tools with MAF Harness file access.
- Do not disable `ApprovalResponseBindingChatClient` to make legacy approvals pass.
- Do not bulk-change every MAF-related package to the same literal version; A2A packages use a preview build suffix.
- Do not downgrade `Microsoft.Extensions.AI`, OpenAI, Azure OpenAI, or MCP packages merely to match the upstream repository's development pins.
- Do not alter required-finalizer governance unless characterization proves a specific part is only compensating for a fixed MAF defect.
- Do not pool complete `AIAgent` object graphs.
- Do not edit opaque MAF session JSON in place as the primary migration mechanism.
- Do not combine optional Harness, AG-UI, declarative workflow, FileMemory, or OpenAI Responses hosting adoption with the compatibility upgrade.
- All source-code comments added during implementation must be in English.

## Most Important Findings

1. The application creates `ChatClientAgentOptions` without opting out of default middleware. MAF 1.15 therefore introduces approval-response binding by default unless a provider factory replaces or bypasses that pipeline.
2. MAF 1.13 made bypassing approval requests for non-approval-required tools opt-in. MAF 1.15 enables that behavior by default and exposes only a disable switch. This is a behavior change even when the code still compiles.
3. CanDoItAll reconstructs pending approval requests from its own persisted record and sends only approval responses on continuation. A legacy 1.13 serialized session does not contain the new MAF binding state, so a direct continuation can be ignored.
4. `HandoffDepthGuardAgent.RunCoreAsync` rebuilds a non-streaming response from streaming updates via `ToAgentResponse()`. That bypasses MAF 1.15's non-streaming terminal-workflow-output projection.
5. The primary runtime also consumes streaming updates and merges them independently. The terminal-output fix must therefore be validated on the real CanDoItAll streaming path, not assumed to apply automatically.
6. CanDoItAll file tools are custom workspace services and separate CanDoItAll.FileTools packages. The Harness `FileAccessStore` opt-in change has no confirmed direct impact, but a full branch grep remains mandatory.
7. A2A is registered in the common hosting composition and uses preview MAF packages. The exact matching 1.15 preview build must be used.
8. The existing immutable preparation/preload architecture must remain intact; 1.15 does not justify pooling live agents or runtime dependencies.

## Bundle Layout

- `inputs/` — original request, repository snapshot, and upstream baseline
- `requirements/` — normalized, testable migration requirements
- `analysis/` — impact analysis, state migration, workaround decisions, and optional opportunities
- `architecture/` — current and target integration maps
- `plan/` — phased implementation, file-level changes, tests, rollout, and observability
- `subbundles/` — eight execution-ready workstreams
- `shared-prompts/` — implementation and independent QA prompts
- `traceability/` — requirement-to-workstream coverage
- `references/` — repository and upstream evidence indexes
- `machine/` — JSON/CSV task data, package properties, discovery scripts, and validation scripts
- `reviews/` — self-review and execution-report template
- `proof/` — proof structure Codex must populate

## Recommended Execution Order

1. `subbundles/01-baseline-discovery-and-1-13-fixtures`
2. `subbundles/02-package-alignment-and-compilation`
3. `subbundles/03-approval-binding-and-state-migration`
4. `subbundles/04-handoff-terminal-output-and-message-ordering`
5. `subbundles/05-session-and-checkpoint-compatibility`
6. `subbundles/06-file-tools-and-capability-security-regression`
7. `subbundles/07-a2a-hosting-and-optional-api-inventory`
8. `subbundles/08-workaround-cleanup-rollout-and-closure`

Do not skip progression gates. A later subbundle cannot compensate for missing 1.13 fixtures or an unresolved approval-state migration.

## Starting Command

Run the platform-appropriate discovery script from the repository root before editing:

```powershell
./.codex/bundles/maf-1-15-upgrade-architecture/machine/grep-discovery.ps1
```

or:

```bash
bash ./.codex/bundles/maf-1-15-upgrade-architecture/machine/grep-discovery.sh
```

When using this ZIP outside the repository, copy its root folder to:

```text
.codex/bundles/maf-1-15-upgrade-architecture
```
