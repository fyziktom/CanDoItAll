# Claude Code execution prompt — SB16

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Remove transitional namespace, ambiguous booleans, fallback construction, and residual broad composition behavior without reopening the runtime monolith.
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
- Work only on SB16; do not opportunistically implement later subbundles.
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
1. Align Runtime.Abstractions namespaces and usings with physical project ownership using a controlled compile-first migration.
2. Replace SuppressApprovalRequirements with an explicit ApprovalExecutionPolicy/lease object and remove stale AllDecisionsApproved compatibility members.
3. Separate immutable execution blueprint from invocation-specific command where it reduces request breadth without duplicating persisted models.
4. Fail fast for required MafAgentRuntimeDependencies instead of constructing defaults. Model optional services through explicit null objects/capability descriptors registered by hosting.
5. Narrow ServiceProviderRegisteredCapabilityServiceSource to an approved registered service catalog; dynamic type-name configuration must not resolve arbitrary host services.
6. Audit remaining MAF references to concrete tools/MCP/skills/documents and move composition-only references outward where practical, without creating cycles.
7. Update mock/scenario decorators and integration test overrides through the narrow ports.
</owned_tasks>
