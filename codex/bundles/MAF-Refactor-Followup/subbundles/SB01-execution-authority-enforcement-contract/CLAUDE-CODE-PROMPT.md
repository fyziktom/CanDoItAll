# Claude Code execution prompt — SB01

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Turn AgentExecutionAuthorityRecord from metadata/audit evidence into the single immutable permission snapshot used by capability planning and tool invocation.
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
- Work only on SB01; do not opportunistically implement later subbundles.
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
1. Introduce a provider-neutral AgentExecutionGovernanceSnapshot or equivalent immutable execution contract containing authority identity, profile/generation, workspace scope, read/mutation grants, allowed operations, capabilities, aliases, policy version, and fingerprint.
2. Persist only its safe projection, but retain the full trusted snapshot through the in-process execution command and continuation lease.
3. At execution start, validate snapshot agent, profile, generation, scope, authority ID/fingerprint, and transient-context digest before creating the runtime.
4. Populate AgentRuntimeContextIntent and AgentRuntimeToolProviderContext from the governance snapshot, not from UI access entries or default-true behavior.
5. Filter mutation tools when MutationAllowed is false and read tools when ReadAllowed is false; invocation-time policy must independently enforce the same snapshot.
6. Thread allowed operations, capability scopes, external-target aliases, and managed-artifact refs from one snapshot; define monotonic intersection with agent configuration and process restrictions.
7. Add a negative production-path test showing that an agent configured with mutation tools cannot mutate when canonical authority is read-only.
</owned_tasks>
