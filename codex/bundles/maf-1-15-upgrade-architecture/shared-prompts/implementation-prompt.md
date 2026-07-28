# Master Implementation Prompt for Codex

```text
You are the senior C#/.NET runtime architect implementing the Microsoft Agent Framework 1.15 migration in:

Repository: fyziktom/CanDoItAll
Branch: agents-loading-refactor
Bundle snapshot SHA: 59f558bc866d39d438b53f5f743dd5e87c2a6253

Use this bundle as the governing execution package.

Primary goal:
Upgrade stable MAF packages from 1.13.0 to 1.15.0 and A2A/Hosting.A2A to 1.15.0-preview.260722.1 while preserving CanDoItAll runtime isolation, custom file/tool security, session behavior, approvals, handoff output correctness, A2A behavior, and the immutable agent-loading/preparation architecture.

Mandatory workflow:
1. Read the root README, requirements, architecture, phase plan, current subbundle README, workaround register, and execution report.
2. Verify actual branch head and classify drift from 59f558bc866d39d438b53f5f743dd5e87c2a6253.
3. Execute subbundles strictly in order and stop at failed progression gates.
4. Before any package edit, run SB01 and capture sanitized 1.13 cross-version fixtures.
5. Make the smallest cohesive change set for the current subbundle only.
6. Add failing-first characterization before removing any workaround.
7. Update proof artifacts and the execution report during the work, not afterward.
8. Use English for all source-code comments.
9. Do not claim a test/build passed without attaching its command and result.
10. Keep credentials, raw secrets, sensitive attachments, and unrestricted tool arguments out of commits and logs.

Non-negotiable architecture constraints:
- Do not pool complete live agents, sessions, mutable tools, MCP clients, provider conversations, approval state, or request authorization.
- Preserve immutable revisioned preparation snapshots.
- Do not replace CanDoItAll workspace/file/command/artifact tools with MAF Harness file access.
- Do not disable ApprovalResponseBindingChatClient.
- Do not trust client-supplied tool name or arguments during approval.
- Do not generate random IDs for missing approval request IDs.
- Do not apply one batch boolean to approvals that are not explicitly identified.
- Do not mutate opaque MAF session JSON as the normal migration mechanism.
- Do not run workflows twice to obtain streaming plus final output.
- Do not sort streamed updates by timestamp.
- Do not remove finalizer governance or application tool policy without specific failing-first proof.
- Do not enable Central Package Management as part of this migration.
- Do not downgrade adjacent MEAI/OpenAI/Azure/MCP packages unless an actual restore/API conflict is proven and tested.
- Do not adopt Harness, AG-UI, declarative workflows, FileMemory, compaction, ToolApprovalAgent, message injection, or OpenAI Responses hosting in the compatibility pass.

Package target:
- Microsoft.Agents.AI = 1.15.0
- Microsoft.Agents.AI.OpenAI = 1.15.0
- Microsoft.Agents.AI.Workflows = 1.15.0
- Microsoft.Agents.AI.A2A = 1.15.0-preview.260722.1
- Microsoft.Agents.AI.Hosting.A2A = 1.15.0-preview.260722.1

Approval migration:
- Keep binding enabled.
- Preserve 1.13 mixed-tool behavior initially by setting DisableApprovalNotRequiredFunctionBypassing = true.
- Add per-request approval decisions, stable IDs, exact-once consumption, versioned compatibility metadata, and fingerprints.
- Prefer reissuing 1.13 pending approvals under 1.15.
- Add a temporary trusted bridge only if required, feature-flagged, fingerprinted, one-time, audited, and expiring.
- Prove function and MCP approval behavior across restart and attack cases.

Workflow migration:
- Characterize direct non-streaming, direct streaming, depth-guard, and full MafAgentRuntime paths.
- Separate intermediate activity from authoritative terminal output.
- Preserve handoff max-depth enforcement without rebuilding the wrong final response.
- Validate caller-visible response and persisted history independently.
- Preserve tool-call/result adjacency, reasoning/text order, author names, IDs, and usage.

Session migration:
- Preserve governed-step isolation, provider-managed conversations, request attachment scrubbing, and bounded persistence.
- Add structured persistence diagnostics.
- Test 1.13-to-1.15 and rollback direction.
- Prove native workflow checkpoint restoration only if the application stores native MAF checkpoint/external request state.

Completion:
- Run deterministic, security, integration, concurrency, full solution, real provider, approval restart, handoff, governed process, and A2A validations.
- Rehearse canary and rollback against copied/sanitized state.
- Close every requirement and workaround row with source/test/proof links.
- Stop and report honestly if a gate cannot pass; do not weaken a safety boundary to force green tests.
```
