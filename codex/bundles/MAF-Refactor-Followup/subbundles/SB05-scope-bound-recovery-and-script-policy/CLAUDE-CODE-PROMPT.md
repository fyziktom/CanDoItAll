# Claude Code execution prompt — SB05

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Eliminate all remaining reads and policy inspections that use MafAgentRuntime construction scope instead of the effective run scope.
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
- Work only on SB05; do not opportunistically implement later subbundles.
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
1. Change MafStreamingTurnExecutor recovery evidence construction to use the run-owned WorkspaceRuntimeServices or a read-only recovery service from that bundle.
2. Remove new WorkspaceFileService(workspaceRoot, workspaceScope) from recovery readers.
3. Create MafScriptPolicyInspectionService per runtime build from the effective WorkspaceExecutionScope, or inject a scope-bound inspection service from the bundle.
4. Verify managed-root mapping, external-target alias resolution, and child-script inspection use the same authority and scope as the invoked command tool.
5. Audit image/document/spreadsheet/MCP helpers for any remaining captured base scope.
6. Add project-turn-on-organization-runtime tests for normal tool read, script inspection, provider-failure recovery, and finalizer recovery.
</owned_tasks>
