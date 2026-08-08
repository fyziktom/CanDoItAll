# Claude Code execution prompt — SB12

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Make MAF a tool-call mapper over a provider-neutral governance pipeline rather than an owner of process facts and hardcoded policy.
</mission>

<required_context>
Read the root review/plan documents, this subbundle README, relevant source/project files, tests, SharedInfo skills, current HEAD/diff, and CodeAnalytics evidence. Do not trust the bundle over changed source.
</required_context>

<constraints>
- Keep source-code comments and identifiers in English.
- Do not add partial-class architecture, nested architecture owners, broad Helpers/Managers, or a Common dumping ground.
- Do not let UI observation, route, prompt text, payload JSON, or current navigation grant authority.
- Do not recapture current UI context or authority during approval continuation.
- Do not duplicate provider, tool, process, or persistence side effects for comparison.
- Do not restore product/process semantics or product module references to MAF.
- Do not make lightweight LLM calls use the full agent runtime.
- Do not add new accepted test failures or exclusions.
- Do not commit, push, or open a PR unless explicitly requested.
- Work only on SB12; do not opportunistically implement later subbundles.
</constraints>

<workflow>
1. Create/update proof manifest and session handoff.
2. Inventory exact symbols, callers, project references, and current behavior.
3. Add characterization/failing tests first.
4. Implement the smallest cohesive owner-boundary change.
5. Build and run focused tests after each cutover step.
6. Exercise at least one negative/fault path.
7. Run architecture guards and inspect the diff for old-path survival.
8. Update proof/handoff continuously.
9. Return the closure output required by the README.
</workflow>

<owned_tasks>
1. Inject IAgentToolInvocationPolicy or a composed IAgentToolGovernancePipeline into MafRuntimeAgentFactory; delete direct new DefaultAgentToolInvocationPolicy().
2. Define ExecutionGovernanceSnapshot with generic resource scope, allowed operations, mutation/read grants, approval policy, external targets, managed refs, and policy fingerprint.
3. Move process-specific interpretation into a Modules.Processes contributor/decorator that narrows the generic snapshot.
4. Remove ProcessRunId/ProcessStepId/product-branch fields from MAF policy construction where they are not telemetry. Map process requirements to generic operation/resource constraints before entering MAF.
5. Make WorkspaceExecutionAuditContext telemetry-only or prove every authorization fact also arrives explicitly in the invocation command.
6. Ensure capability filtering and invocation policy use the same monotonic decision model and cannot be weakened by catalog order.
7. Expand architecture guards beyond a short token list so new process fields cannot return under different names.
</owned_tasks>
